using System.Security.Claims;
using BTSS.IAR.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BTSS.IAR.Api.Auth;

public sealed class HumanLoginRequest
{
    public HumanLoginRequest(string username, string password, int? agencyId = null)
    {
        UserName = username;
        Password = password;
        AgencyId = agencyId;
    }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public int? AgencyId { get; set; }
}
public sealed record KioskAdminSessionDto(int UserId, string UserName, string DisplayName, bool IsSuperUser, int? ActiveAgencyId, string[] Permissions, AgencyMembershipDto[] Agencies);
public sealed record AgencyMembershipDto(int AgencyId, string AgencyCode, string AgencyName, bool IsDefault);
public sealed record CurrentUserDto(int UserId, string UserName, string DisplayName, bool IsSuperUser, int? ActiveAgencyId, string[] Permissions, AgencyMembershipDto[] Agencies);
public sealed record SwitchAgencyRequest(int AgencyId);

public static class PermissionCatalog
{
    public const string AdminUsers = "admin.users";
    public const string AdminAgencies = "admin.agencies";
    public const string AdminScopes = "admin.scopes";
    public const string AdminDevices = "admin.devices";
    public const string AdminSettings = "admin.settings";
    public const string AdminApiClients = "admin.api-clients";
    public const string AdminDiagnostics = "admin.diagnostics";
    public const string ReportRun = "report.run";
    public const string ReportDesign = "report.design";
    public const string KioskAdmin = "kiosk.admin";

    public static readonly (string Code, string Description)[] All =
    {
        (AdminUsers, "Manage users, memberships, and role assignments."),
        (AdminAgencies, "Manage agencies and active tenancy context."),
        (AdminScopes, "Manage scope catalogs and assignable scopes."),
        (AdminDevices, "Manage device registrations and assignments."),
        (AdminSettings, "Manage global and per-device settings."),
        (AdminApiClients, "Manage API clients and machine credentials."),
        (AdminDiagnostics, "View diagnostics and operational state."),
        (ReportRun, "Run agency-scoped reporting queries and saved reports."),
        (ReportDesign, "Create, edit, and share reporting definitions."),
        (KioskAdmin, "Access kiosk admin/setup mode.")
    };
}

public static class RoleCatalog
{
    public const string SuperUser = "SuperUser";
    public const string AgencyAdmin = "AgencyAdmin";
    public const string KioskAdmin = "KioskAdmin";
}

public sealed class HumanAuthService
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<UserAccount> _hasher = new();

    public HumanAuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task SeedDefaultsAsync(CancellationToken ct = default)
    {
        foreach (var perm in PermissionCatalog.All)
        {
            if (!await _db.Permissions.AnyAsync(x => x.Code == perm.Code, ct))
                _db.Permissions.Add(new PermissionDefinition { Code = perm.Code, Description = perm.Description });
        }

        if (!await _db.Roles.AnyAsync(x => x.Name == RoleCatalog.SuperUser, ct))
            _db.Roles.Add(new RoleDefinition { Name = RoleCatalog.SuperUser, Description = "Global super user", IsSystemRole = true });
        if (!await _db.Roles.AnyAsync(x => x.Name == RoleCatalog.AgencyAdmin, ct))
            _db.Roles.Add(new RoleDefinition { Name = RoleCatalog.AgencyAdmin, Description = "Agency-scoped admin", IsSystemRole = true });
        if (!await _db.Roles.AnyAsync(x => x.Name == RoleCatalog.KioskAdmin, ct))
            _db.Roles.Add(new RoleDefinition { Name = RoleCatalog.KioskAdmin, Description = "Kiosk admin/setup access", IsSystemRole = true });

        if (!await _db.Agencies.AnyAsync(ct))
            _db.Agencies.Add(new Agency { Code = "DEFAULT", Name = "Default Agency" });

        await _db.SaveChangesAsync(ct);

        var roles = await _db.Roles.ToDictionaryAsync(x => x.Name, ct);
        var perms = await _db.Permissions.ToDictionaryAsync(x => x.Code, ct);

        await GrantRolePermissionsAsync(roles[RoleCatalog.SuperUser].Id, perms.Keys.ToArray(), ct);
        await GrantRolePermissionsAsync(roles[RoleCatalog.AgencyAdmin].Id,
            PermissionCatalog.AdminUsers,
            PermissionCatalog.AdminAgencies,
            PermissionCatalog.AdminScopes,
            PermissionCatalog.AdminDevices,
            PermissionCatalog.AdminSettings,
            PermissionCatalog.AdminApiClients,
            PermissionCatalog.AdminDiagnostics,
            PermissionCatalog.ReportRun,
            PermissionCatalog.ReportDesign,
            PermissionCatalog.KioskAdmin,
            ct);
        await GrantRolePermissionsAsync(roles[RoleCatalog.KioskAdmin].Id,
            PermissionCatalog.KioskAdmin,
            PermissionCatalog.AdminDevices,
            PermissionCatalog.AdminDiagnostics,
            PermissionCatalog.ReportRun,
            ct);

        if (!await _db.Users.AnyAsync(ct))
        {
            var agency = await _db.Agencies.OrderBy(x => x.Id).FirstAsync(ct);
            var user = new UserAccount
            {
                UserName = "superadmin",
                NormalizedUserName = "SUPERADMIN",
                Email = "superadmin@local",
                DisplayName = "System Super User",
                IsSuperUser = true,
                ActiveAgencyId = agency.Id
            };
            user.PasswordHash = _hasher.HashPassword(user, "ChangeMe123!");
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            _db.UserAgencies.Add(new UserAgencyMembership { UserId = user.Id, AgencyId = agency.Id, IsDefault = true, IsEnabled = true });
            _db.UserRoles.Add(new UserRoleAssignment { UserId = user.Id, RoleId = roles[RoleCatalog.SuperUser].Id });
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserAccount?> ValidateCredentialsAsync(string? userName, string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            return null;

        var normalized = userName.Trim().ToUpperInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(x => x.NormalizedUserName == normalized && x.IsEnabled, ct);
        if (user is null)
            return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        user.LastPasswordVerificationResult = result;
        if (result == PasswordVerificationResult.Failed)
            return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return user;
    }

    public async Task<CurrentUserDto> BuildCurrentUserAsync(UserAccount user, int? requestedAgencyId = null, CancellationToken ct = default)
    {
        var memberships = await (from ua in _db.UserAgencies
                                 join a in _db.Agencies on ua.AgencyId equals a.Id
                                 where ua.UserId == user.Id && ua.IsEnabled && a.IsEnabled
                                 orderby ua.IsDefault descending, a.Name
                                 select new AgencyMembershipDto(a.Id, a.Code, a.Name, ua.IsDefault))
                                 .ToArrayAsync(ct);

        var activeAgencyId = requestedAgencyId
            ?? user.ActiveAgencyId
            ?? memberships.FirstOrDefault()?.AgencyId;

        var permissions = await ResolvePermissionsAsync(user.Id, user.IsSuperUser, activeAgencyId, ct);
        return new CurrentUserDto(user.Id, user.UserName, user.DisplayName, user.IsSuperUser, activeAgencyId, permissions, memberships);
    }

    public async Task<string[]> ResolvePermissionsAsync(int userId, bool isSuperUser, int? activeAgencyId, CancellationToken ct = default)
    {
        if (isSuperUser)
            return PermissionCatalog.All.Select(x => x.Code).OrderBy(x => x).ToArray();

        var rolePermissionQuery =
            from ur in _db.UserRoles
            join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in _db.Permissions on rp.PermissionId equals p.Id
            where ur.UserId == userId && (ur.AgencyId == null || ur.AgencyId == activeAgencyId)
            select p.Code;

        var perms = await rolePermissionQuery.Distinct().ToListAsync(ct);
        var overrides = await (from up in _db.UserPermissions
                               join p in _db.Permissions on up.PermissionId equals p.Id
                               where up.UserId == userId && (up.AgencyId == null || up.AgencyId == activeAgencyId)
                               select new { p.Code, up.IsGranted }).ToListAsync(ct);
        foreach (var o in overrides)
        {
            if (o.IsGranted && !perms.Contains(o.Code, StringComparer.OrdinalIgnoreCase)) perms.Add(o.Code);
            if (!o.IsGranted) perms.RemoveAll(x => string.Equals(x, o.Code, StringComparison.OrdinalIgnoreCase));
        }
        perms.Sort(StringComparer.OrdinalIgnoreCase);
        return perms.ToArray();
    }

    public async Task SignInAsync(HttpContext http, CurrentUserDto currentUser)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, currentUser.UserId.ToString()),
            new(ClaimTypes.Name, currentUser.UserName),
            new("display_name", currentUser.DisplayName),
            new("is_super_user", currentUser.IsSuperUser ? "true" : "false")
        };
        if (currentUser.ActiveAgencyId.HasValue)
            claims.Add(new("active_agency_id", currentUser.ActiveAgencyId.Value.ToString()));
        claims.AddRange(currentUser.Permissions.Select(x => new Claim("permission", x)));
        claims.AddRange(currentUser.Agencies.Select(x => new Claim("agency_membership", x.AgencyId.ToString())));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
        });
    }

    private async Task GrantRolePermissionsAsync(int roleId, params object[] values)
    {
        CancellationToken ct = CancellationToken.None;
        var codes = new List<string>();
        foreach (var value in values)
        {
            if (value is CancellationToken token) ct = token;
            else if (value is string s) codes.Add(s);
        }
        var permissionIds = await _db.Permissions.Where(x => codes.Contains(x.Code)).Select(x => x.Id).ToListAsync(ct);
        foreach (var permissionId in permissionIds)
        {
            if (!await _db.RolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, ct))
                _db.RolePermissions.Add(new RolePermissionAssignment { RoleId = roleId, PermissionId = permissionId });
        }
        await _db.SaveChangesAsync(ct);
    }
}

public static class HumanAuthHttpExtensions
{
    public static bool HasPermission(this ClaimsPrincipal user, string permission)
        => user.Claims.Any(x => x.Type == "permission" && string.Equals(x.Value, permission, StringComparison.OrdinalIgnoreCase));
}
