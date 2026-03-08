using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using BTSS.IAR.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BTSS.IAR.Api.Auth;

public static class HumanAdminEndpointExtensions
{
    public static IEndpointRouteBuilder MapHumanAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/lookups/scopes", (HttpContext http) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminScopes, PermissionCatalog.AdminApiClients, PermissionCatalog.AdminDiagnostics))
                return Results.Forbid();

            var items = new[]
            {
                new ScopeLookupDto("clients.read", "View API clients and machine registrations.", "Clients"),
                new ScopeLookupDto("clients.write", "Create and update API clients.", "Clients"),
                new ScopeLookupDto("display.read", "Read display registrations and screen metadata.", "Display"),
                new ScopeLookupDto("display.write", "Create and update display registrations.", "Display"),
                new ScopeLookupDto("device-settings.read", "Read machine and device settings.", "Devices"),
                new ScopeLookupDto("device-settings.write", "Update machine and device settings.", "Devices"),
                new ScopeLookupDto("global-settings.read", "Read tenant-level global settings.", "Settings"),
                new ScopeLookupDto("global-settings.write", "Update tenant-level global settings.", "Settings"),
                new ScopeLookupDto("kiosk.commands", "Issue kiosk and service commands.", "Runtime"),
                new ScopeLookupDto("service.poll", "Poll for service workloads and sync data.", "Runtime"),
                new ScopeLookupDto("report.access", "Run incident and pivot reports.", "Reports"),
                new ScopeLookupDto("call.ingest", "Send incident ingest payloads.", "Ingest")
            };

            return Results.Ok(items.OrderBy(x => x.Category).ThenBy(x => x.Code));
        }).RequireAuthorization();

        app.MapGet("/api/lookups/permissions", (HttpContext http) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminUsers, PermissionCatalog.AdminScopes, PermissionCatalog.AdminAgencies))
                return Results.Forbid();
            return Results.Ok(PermissionCatalog.All.Select(x => new { x.Code, x.Description }).OrderBy(x => x.Code));
        }).RequireAuthorization();

        app.MapGet("/api/lookups/agencies", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (!(http.User.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
            var isSuper = http.User.IsInRole(RoleCatalog.SuperUser) || http.User.Claims.Any(x => x.Type == "is_super_user" && x.Value == "true");
            var memberships = http.User.Claims.Where(x => x.Type == "agency_membership").Select(x => int.TryParse(x.Value, out var id) ? id : (int?)null).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
            var query = db.Agencies.Where(x => x.IsEnabled);
            if (!isSuper)
                query = query.Where(x => memberships.Contains(x.Id));
            var items = await query.OrderBy(x => x.Name).Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        app.MapGet("/api/lookups/status-codes", (HttpContext http) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminDiagnostics, PermissionCatalog.AdminUsers, PermissionCatalog.AdminAgencies))
                return Results.Forbid();
            var items = new[]
            {
                new StatusCodeLookupDto("DP", "Dispatched", "Unit dispatched"),
                new StatusCodeLookupDto("ER", "Enroute", "Unit responding"),
                new StatusCodeLookupDto("OS", "Arrived", "Unit on scene"),
                new StatusCodeLookupDto("TR", "Transport Begin", "Transport started"),
                new StatusCodeLookupDto("TC", "Transport Complete", "Transport completed"),
                new StatusCodeLookupDto("CL", "Cleared", "Unit cleared"),
                new StatusCodeLookupDto("CU", "Cleared", "Unit cleared"),
                new StatusCodeLookupDto("AV", "In Quarters", "Unit available / in quarters"),
                new StatusCodeLookupDto("AK", "In Quarters", "Acknowledged / available"),
                new StatusCodeLookupDto("AM", "In Quarters", "Available / quarters")
            };
            return Results.Ok(items);
        }).RequireAuthorization();

        app.MapGet("/admin/dashboard", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (!(http.User.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
            var activeAgencyId = GetActiveAgencyId(http.User);
            var today = DateTime.UtcNow.Date;
            var incidents = db.Incidents.AsQueryable();
            var devices = db.Devices.AsQueryable();
            var sync = db.IncidentSyncLog.AsQueryable();
            var reports = db.SavedReports.AsQueryable();
            if (activeAgencyId.HasValue)
            {
                incidents = incidents.Where(x => x.AgencyPrimaryId == activeAgencyId.Value || db.IncidentAgencies.Any(ia => ia.IncidentId == x.Id && ia.AgencyId == activeAgencyId.Value));
                devices = devices.Where(x => x.AgencyId == activeAgencyId.Value);
                sync = sync.Where(x => x.AgencyId == activeAgencyId.Value);
                reports = reports.Where(x => x.AgencyId == activeAgencyId.Value);
            }

            var openIncidents = await incidents.CountAsync(x => x.ClosedAtUtc == null && !string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase), ct);
            var closedToday = await incidents.CountAsync(x => x.ClosedAtUtc != null && x.ClosedAtUtc >= today, ct);
            var unitsActive = await db.IncidentUnits.CountAsync(x => x.CurrentStatus != null && x.CurrentStatus != "In Quarters" && x.CurrentStatus != "Cleared", ct);
            var devicesRegistered = await devices.CountAsync(ct);
            var offlineIndicators = await devices.CountAsync(x => x.UpdatedAtUtc < DateTime.UtcNow.AddMinutes(-15), ct);
            var savedReports = await reports.CountAsync(ct);
            var pendingSync = await sync.CountAsync(x => x.AckedAtUtc == null, ct);
            var alerts = offlineIndicators + await db.IncidentSyncLog.CountAsync(x => x.AckedAtUtc == null && x.ChangedAtUtc < DateTime.UtcNow.AddMinutes(-10), ct);
            return Results.Ok(new DashboardSummaryDto(activeAgencyId, openIncidents, closedToday, unitsActive, devicesRegistered, offlineIndicators, savedReports, pendingSync, alerts));
        }).RequireAuthorization();

        app.MapGet("/admin/agencies", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminAgencies)) return Results.Forbid();
            var agencies = await db.Agencies.OrderBy(x => x.Name).ToListAsync(ct);
            var userCounts = await db.UserAgencies.GroupBy(x => x.AgencyId).Select(g => new { AgencyId = g.Key, Count = g.Count(x => x.IsEnabled) }).ToDictionaryAsync(x => x.AgencyId, x => x.Count, ct);
            var deviceCounts = await db.Devices.GroupBy(x => x.AgencyId).Select(g => new { AgencyId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.AgencyId, x => x.Count, ct);
            var incidentCounts = await db.IncidentAgencies.GroupBy(x => x.AgencyId).Select(g => new { AgencyId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.AgencyId, x => x.Count, ct);
            return Results.Ok(agencies.Select(x => new AgencyAdminDto(x.Id, x.Code, x.Name, x.IsEnabled, userCounts.GetValueOrDefault(x.Id), deviceCounts.GetValueOrDefault(x.Id), incidentCounts.GetValueOrDefault(x.Id))));
        }).RequireAuthorization();

        app.MapPost("/admin/agencies", async (HttpContext http, AppDbContext db, AgencyUpsertRequest req, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminAgencies)) return Results.Forbid();
            var entity = new Agency { Code = req.Code.Trim(), Name = req.Name.Trim(), IsEnabled = req.IsEnabled, CreatedAtUtc = DateTime.UtcNow };
            db.Agencies.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Ok(entity);
        }).RequireAuthorization();

        app.MapPut("/admin/agencies/{id:int}", async (HttpContext http, AppDbContext db, int id, AgencyUpsertRequest req, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminAgencies)) return Results.Forbid();
            var entity = await db.Agencies.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return Results.NotFound();
            entity.Code = req.Code.Trim();
            entity.Name = req.Name.Trim();
            entity.IsEnabled = req.IsEnabled;
            await db.SaveChangesAsync(ct);
            return Results.Ok(entity);
        }).RequireAuthorization();

        app.MapGet("/admin/roles", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminUsers)) return Results.Forbid();
            var permissionMap = await (from rp in db.RolePermissions
                                       join p in db.Permissions on rp.PermissionId equals p.Id
                                       select new { rp.RoleId, p.Code }).ToListAsync(ct);
            var roles = await db.Roles.OrderBy(x => x.Name).ToListAsync(ct);
            return Results.Ok(roles.Select(r => new RoleAdminDto(r.Id, r.Name, r.Description, r.IsSystemRole, permissionMap.Where(x => x.RoleId == r.Id).Select(x => x.Code).OrderBy(x => x).ToArray())));
        }).RequireAuthorization();

        app.MapPost("/admin/roles", async (HttpContext http, AppDbContext db, RoleUpsertRequest req, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminUsers)) return Results.Forbid();
            var entity = new RoleDefinition { Name = req.Name.Trim(), Description = req.Description.Trim(), IsSystemRole = req.IsSystemRole };
            db.Roles.Add(entity);
            await db.SaveChangesAsync(ct);
            await ReplaceRolePermissionsAsync(db, entity.Id, req.PermissionCodes, ct);
            return Results.Ok(entity);
        }).RequireAuthorization();

        app.MapPut("/admin/roles/{id:int}", async (HttpContext http, AppDbContext db, int id, RoleUpsertRequest req, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminUsers)) return Results.Forbid();
            var entity = await db.Roles.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return Results.NotFound();
            entity.Name = req.Name.Trim();
            entity.Description = req.Description.Trim();
            if (!entity.IsSystemRole) entity.IsSystemRole = req.IsSystemRole;
            await db.SaveChangesAsync(ct);
            await ReplaceRolePermissionsAsync(db, entity.Id, req.PermissionCodes, ct);
            return Results.Ok(entity);
        }).RequireAuthorization();

        app.MapGet("/admin/users", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminUsers)) return Results.Forbid();
            var users = await db.Users.OrderBy(x => x.UserName).ToListAsync(ct);
            var memberships = await db.UserAgencies.ToListAsync(ct);
            var roles = await db.UserRoles.ToListAsync(ct);
            var rolePermissions = await (from ur in db.UserRoles
                                         join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
                                         join p in db.Permissions on rp.PermissionId equals p.Id
                                         select new { ur.UserId, p.Code, ur.AgencyId }).ToListAsync(ct);
            return Results.Ok(users.Select(u => new UserAdminDto(
                u.Id,
                u.UserName,
                u.Email,
                u.DisplayName,
                u.IsEnabled,
                u.IsSuperUser,
                u.ActiveAgencyId,
                memberships.Where(x => x.UserId == u.Id).Select(x => new UserAgencyAssignmentDto(x.AgencyId, x.IsDefault, x.IsEnabled)).ToArray(),
                roles.Where(x => x.UserId == u.Id).Select(x => new UserRoleAssignmentDto(x.RoleId, x.AgencyId)).ToArray(),
                u.IsSuperUser ? PermissionCatalog.All.Select(x => x.Code).OrderBy(x => x).ToArray() : rolePermissions.Where(x => x.UserId == u.Id).Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray()
            )));
        }).RequireAuthorization();

        app.MapPost("/admin/users", async (HttpContext http, AppDbContext db, UserUpsertRequest req, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminUsers)) return Results.Forbid();
            var hasher = new PasswordHasher<UserAccount>();
            var user = new UserAccount
            {
                UserName = req.UserName.Trim(),
                NormalizedUserName = req.UserName.Trim().ToUpperInvariant(),
                Email = req.Email.Trim(),
                DisplayName = req.DisplayName.Trim(),
                IsEnabled = req.IsEnabled,
                IsSuperUser = req.IsSuperUser,
                ActiveAgencyId = req.ActiveAgencyId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            user.PasswordHash = hasher.HashPassword(user, string.IsNullOrWhiteSpace(req.Password) ? "ChangeMe123!" : req.Password!);
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            await ReplaceUserMembershipsAsync(db, user.Id, req.Agencies, ct);
            await ReplaceUserRolesAsync(db, user.Id, req.Roles, ct);
            return Results.Ok(user);
        }).RequireAuthorization();

        app.MapPut("/admin/users/{id:int}", async (HttpContext http, AppDbContext db, int id, UserUpsertRequest req, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminUsers)) return Results.Forbid();
            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (user is null) return Results.NotFound();
            user.UserName = req.UserName.Trim();
            user.NormalizedUserName = req.UserName.Trim().ToUpperInvariant();
            user.Email = req.Email.Trim();
            user.DisplayName = req.DisplayName.Trim();
            user.IsEnabled = req.IsEnabled;
            user.IsSuperUser = req.IsSuperUser;
            user.ActiveAgencyId = req.ActiveAgencyId;
            user.UpdatedAtUtc = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(req.Password))
            {
                var hasher = new PasswordHasher<UserAccount>();
                user.PasswordHash = hasher.HashPassword(user, req.Password!);
            }
            await db.SaveChangesAsync(ct);
            await ReplaceUserMembershipsAsync(db, user.Id, req.Agencies, ct);
            await ReplaceUserRolesAsync(db, user.Id, req.Roles, ct);
            return Results.Ok(user);
        }).RequireAuthorization();

        app.MapGet("/admin/devices", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminDevices, PermissionCatalog.AdminDiagnostics)) return Results.Forbid();
            var clients = await db.ApiClients.ToDictionaryAsync(x => x.Id, x => x.Name ?? x.ClientId, ct);
            var agencies = await db.Agencies.ToDictionaryAsync(x => x.Id, x => x.Name, ct);
            var settings = await db.DeviceSettings.ToListAsync(ct);
            var devices = await db.Devices.OrderByDescending(x => x.UpdatedAtUtc).ToListAsync(ct);
            return Results.Ok(devices.Select(d => new DeviceAdminDto(d.Id, d.ApiClientId, d.AgencyId, d.DeviceId, d.DeviceName, d.MachineName, d.DeviceType, d.IsEnabled, d.UpdatedAtUtc,
                TryParseJson(settings.FirstOrDefault(x => x.DeviceId == d.DeviceId)?.SettingsJson), clients.GetValueOrDefault(d.ApiClientId), agencies.GetValueOrDefault(d.AgencyId))));
        }).RequireAuthorization();

        app.MapGet("/admin/settings", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminSettings)) return Results.Forbid();
            var activeAgencyId = GetActiveAgencyId(http.User);
            if (!activeAgencyId.HasValue) return Results.Ok(new SettingsEnvelopeDto(0, new JsonObject(), null));
            var global = await db.GlobalSettings.FirstOrDefaultAsync(x => x.AgencyId == activeAgencyId.Value, ct);
            return Results.Ok(new SettingsEnvelopeDto(activeAgencyId.Value, TryParseJson(global?.SettingsJson) ?? new JsonObject(), global?.UpdatedAtUtc));
        }).RequireAuthorization();

        app.MapPut("/admin/settings", async (HttpContext http, AppDbContext db, SettingsEnvelopeDto req, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminSettings)) return Results.Forbid();
            var agencyId = req.AgencyId != 0 ? req.AgencyId : GetActiveAgencyId(http.User) ?? 0;
            if (agencyId == 0) return Results.BadRequest(new { message = "No active agency." });
            var entity = await db.GlobalSettings.FirstOrDefaultAsync(x => x.AgencyId == agencyId, ct);
            if (entity is null)
            {
                entity = new GlobalSettingEntity { AgencyId = agencyId, SettingsJson = req.Settings.ToJsonString(), UpdatedAtUtc = DateTime.UtcNow };
                db.GlobalSettings.Add(entity);
            }
            else
            {
                entity.SettingsJson = req.Settings.ToJsonString();
                entity.UpdatedAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new SettingsEnvelopeDto(agencyId, req.Settings, entity.UpdatedAtUtc));
        }).RequireAuthorization();

        app.MapGet("/admin/diagnostics", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (!HasPermission(http.User, PermissionCatalog.AdminDiagnostics)) return Results.Forbid();
            var activeAgencyId = GetActiveAgencyId(http.User);
            var sync = db.IncidentSyncLog.AsQueryable();
            if (activeAgencyId.HasValue) sync = sync.Where(x => x.AgencyId == activeAgencyId.Value);
            var recentSync = await sync.OrderByDescending(x => x.ChangedAtUtc).Take(50).Select(x => new { x.Id, x.IncidentId, x.AgencyId, x.Scope, x.ChangeType, x.ChangedAtUtc, x.AckedAtUtc, x.DeviceId, x.Notes }).ToListAsync(ct);
            var clients = await db.ApiClients.OrderBy(x => x.Name).Select(x => new { x.Id, x.ClientId, x.Name, x.AgencyId, x.IsEnabled, x.ClientSecretVersion, x.ClientSecretRotatedAtUtc, x.UpdatedAtUtc, Scopes = x.AllowedScopes }).ToListAsync(ct);
            var devices = await db.Devices.OrderByDescending(x => x.UpdatedAtUtc).Take(50).Select(x => new { x.Id, x.DeviceId, x.DeviceName, x.MachineName, x.DeviceType, x.AgencyId, x.IsEnabled, x.UpdatedAtUtc }).ToListAsync(ct);
            return Results.Ok(new { activeAgencyId, recentSync, clients, devices });
        }).RequireAuthorization();

        return app;
    }

    private static async Task ReplaceRolePermissionsAsync(AppDbContext db, int roleId, IEnumerable<string>? codes, CancellationToken ct)
    {
        var existing = await db.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync(ct);
        db.RolePermissions.RemoveRange(existing);
        var normalized = (codes ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (normalized.Length > 0)
        {
            var permissionIds = await db.Permissions.Where(x => normalized.Contains(x.Code)).Select(x => x.Id).ToListAsync(ct);
            db.RolePermissions.AddRange(permissionIds.Select(id => new RolePermissionAssignment { RoleId = roleId, PermissionId = id }));
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task ReplaceUserMembershipsAsync(AppDbContext db, int userId, IEnumerable<UserAgencyAssignmentDto>? items, CancellationToken ct)
    {
        var existing = await db.UserAgencies.Where(x => x.UserId == userId).ToListAsync(ct);
        db.UserAgencies.RemoveRange(existing);
        var memberships = (items ?? Array.Empty<UserAgencyAssignmentDto>())
            .GroupBy(x => x.AgencyId)
            .Select(g => g.First())
            .Select(x => new UserAgencyMembership { UserId = userId, AgencyId = x.AgencyId, IsDefault = x.IsDefault, IsEnabled = x.IsEnabled })
            .ToArray();
        if (memberships.Length > 0) db.UserAgencies.AddRange(memberships);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ReplaceUserRolesAsync(AppDbContext db, int userId, IEnumerable<UserRoleAssignmentDto>? items, CancellationToken ct)
    {
        var existing = await db.UserRoles.Where(x => x.UserId == userId).ToListAsync(ct);
        db.UserRoles.RemoveRange(existing);
        var roles = (items ?? Array.Empty<UserRoleAssignmentDto>())
            .GroupBy(x => new { x.RoleId, x.AgencyId })
            .Select(g => g.First())
            .Select(x => new UserRoleAssignment { UserId = userId, RoleId = x.RoleId, AgencyId = x.AgencyId })
            .ToArray();
        if (roles.Length > 0) db.UserRoles.AddRange(roles);
        await db.SaveChangesAsync(ct);
    }

    private static JsonObject? TryParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json) as JsonObject; } catch { return null; }
    }

    private static int? GetActiveAgencyId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst("active_agency_id")?.Value;
        return int.TryParse(claim, out var value) ? value : null;
    }

    private static bool HasPermission(ClaimsPrincipal user, params string[] codes)
        => user.Identity?.IsAuthenticated == true && (user.Claims.Any(x => x.Type == "is_super_user" && x.Value == "true") || user.Claims.Any(x => x.Type == "permission" && codes.Contains(x.Value, StringComparer.OrdinalIgnoreCase)));
}
