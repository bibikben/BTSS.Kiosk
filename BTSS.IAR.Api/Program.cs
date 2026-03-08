using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BTSS.IAR.Api.Data;
using BTSS.IAR.Api.Models;
using BTSS.IAR.Api.Models.Dtos;
using BTSS.IAR.Record.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Sql")
             ?? "Server=.\\SQLEXPRESS;Database=BTSS_IAR;Trusted_Connection=True;TrustServerCertificate=True";
    opt.UseSqlServer(cs);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BTSS IAR API",
        Version = "v1",
        Description = "OAuth2 client-credentials only API for BTSS IAR operations."
    });

    var xmlName = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlName);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri("/connect/token", UriKind.Relative),
                Scopes = ScopeCatalog.All.ToDictionary(s => s, s => s)
            }
        }
    });

    //c.AddSecurityRequirement(new OpenApiSecurityRequirement
    //{
    //    {
    //        new OpenApiSecurityScheme
    //        {
    //            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
    //        },
    //        ScopeCatalog.All.ToArray()
    //    }
    //});
});

var jwtKey = builder.Configuration["Auth:JwtKey"] ?? "CHANGE_ME__DEV_ONLY__PLEASE_SET_IN_APPSETTINGS";
var jwtIssuer = builder.Configuration["Auth:Issuer"] ?? "btss-iar-api";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("scope:ingest", policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasAnyScope(ctx.User, ScopeCatalog.Ingest)));
    options.AddPolicy("scope:service", policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasAnyScope(ctx.User, ScopeCatalog.ServicePolling, ScopeCatalog.ReportAccess)));
    options.AddPolicy("scope:clients.read", policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasAnyScope(ctx.User, ScopeCatalog.ClientsRead)));
    options.AddPolicy("scope:clients.write", policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasAnyScope(ctx.User, ScopeCatalog.ClientsWrite)));
    options.AddPolicy("scope:display.read", policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasAnyScope(ctx.User, ScopeCatalog.DisplayRead)));
    options.AddPolicy("scope:display.write", policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasAnyScope(ctx.User, ScopeCatalog.DisplayWrite)));
    options.AddPolicy("scope:device.read", policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasAnyScope(ctx.User, ScopeCatalog.DeviceRead)));
    options.AddPolicy("scope:device.write", policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasAnyScope(ctx.User, ScopeCatalog.DeviceWrite)));
    options.AddPolicy("scope:global.read", policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasAnyScope(ctx.User, ScopeCatalog.GlobalRead)));
    options.AddPolicy("scope:global.write", policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasAnyScope(ctx.User, ScopeCatalog.GlobalWrite)));
    options.AddPolicy("scope:kiosk.commands", policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => HasAnyScope(ctx.User, ScopeCatalog.KioskCommands)));
});

builder.Services.AddSingleton(new TokenIssuer(jwtIssuer, signingKey));
builder.Services.AddScoped<LookupUpsertService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("iar-ingest", httpContext =>
    {
        var agency = httpContext.User?.FindFirst("agency_id")?.Value;
        var key = !string.IsNullOrWhiteSpace(agency)
            ? $"agency:{agency}"
            : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: key,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    await EnsureEpic5SchemaAsync(db);
    await EnsureEpic10SchemaAsync(db);
    await EnsureEpic13SchemaAsync(db);
    await EnsureIngestSchemaAsync(db);
}

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.OAuthClientId("swagger-ui");
        c.OAuthUsePkce();
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapPost("/connect/token", async (HttpRequest request, AppDbContext db, TokenIssuer issuer) =>
{
    TokenRequest? req;
    if (request.HasFormContentType)
    {
        var form = await request.ReadFormAsync();
        req = new TokenRequest
        {
            ClientId = form["client_id"].ToString(),
            ClientSecret = form["client_secret"].ToString(),
            GrantType = string.IsNullOrWhiteSpace(form["grant_type"]) ? "client_credentials" : form["grant_type"].ToString(),
            Scope = form["scope"].ToString()
        };
        if (string.IsNullOrEmpty(req.ClientId)) req.ClientId = form["clientId"].ToString();
        if (string.IsNullOrEmpty(req.ClientSecret)) req.ClientSecret = form["clientSecret"].ToString();
        if (string.IsNullOrEmpty(req.GrantType)) req.GrantType = form["grantType"].ToString();

    }
    else
    {
        req = await request.ReadFromJsonAsync<TokenRequest>();
    }

    if (req == null)
        return Results.BadRequest(new { error = "invalid_request" });
    if (!string.Equals(req.GrantType, "client_credentials", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "unsupported_grant_type" });

    var client = await db.ApiClients.Include(x => x.SourceSystem).FirstOrDefaultAsync(c => c.ClientId == req.ClientId);
    if (client == null || !client.IsEnabled || !client.VerifyClientSecret(req.ClientSecret))
        return Results.Unauthorized();

    var requestedScopes = ParseScopes(req.Scope);
    var allowedScopes = client.AllowedScopes.Count == 0 ? ScopeCatalog.DefaultClientScopes : client.AllowedScopes;

    if (requestedScopes.Count == 0)
        requestedScopes = allowedScopes.ToList();

    var denied = requestedScopes.Except(allowedScopes, StringComparer.OrdinalIgnoreCase).ToArray();
    if (denied.Length > 0)
        return Results.BadRequest(new { error = "invalid_scope", error_description = $"Requested scopes are not allowed: {string.Join(", ", denied)}" });

    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    var claims = new List<Claim>
    {
        new("client_id", client.ClientId),
        new("agency_id", client.AgencyId.ToString()),
        new("api_client_id", client.Id.ToString()),
        new("source_system", client.EffectiveSourceSystemCode),
        new("scope", string.Join(' ', requestedScopes))
    };

    claims.AddRange(requestedScopes.Select(s => new Claim("scope", s)));

    var token = issuer.IssueToken(claims, expiresMinutes: 60);

    return Results.Ok(new
    {
        access_token = token,
        token_type = "Bearer",
        expires_in = 3600,
        scope = string.Join(' ', requestedScopes)
    });
})
.Accepts<TokenRequest>("application/json")
.Accepts<IFormCollection>("application/x-www-form-urlencoded")
.WithName("OAuthToken")
.WithSummary("Issue OAuth2 access token")
.WithDescription("Client-credentials only token endpoint.");

if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/seedClient", async (AppDbContext db, LookupUpsertService lookups, SeedClientRequest req) =>
    {
        var existing = await db.ApiClients.FirstOrDefaultAsync(x => x.ClientId == req.ClientId);
        if (existing != null) return Results.Conflict(new { message = "ClientId already exists" });
        if (string.IsNullOrWhiteSpace(req.ClientSecret)) return Results.BadRequest(new { message = "ClientSecret is required." });

        var sourceSystemId = await lookups.EnsureSourceSystemAsync(req.SourceSystemCode ?? "IAR");
        var client = new ApiClient
        {
            AgencyId = req.AgencyId,
            ClientId = req.ClientId,
            Name = req.Name ?? req.ClientId,
            SourceSystemId = sourceSystemId,
            GlobalSettingsJson = JsonSerializer.Serialize(req.GlobalSettings ?? new JsonObject()),
            AllowedScopesJson = JsonSerializer.Serialize(NormalizeScopes(req.AllowedScopes)),
            IsEnabled = true,
            UpdatedAtUtc = DateTime.UtcNow
        };
        client.SetClientSecret(req.ClientSecret);

        db.ApiClients.Add(client);
        await db.SaveChangesAsync();
        return Results.Ok(new { id = client.Id, message = "Seeded" });
    })
    .WithSummary("Development-only client seeding");
}

var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/deprecations", () => Results.Ok(new
{
    removedRoutes = new[]
    {
        "/api/receiveCallDetails/iar",
        "/api/receiveCallDetails/emailText",
        "/api/receiveCallDetails",
        "/api/checkForClose",
        "/api/getCallRecord",
        "/api/getListOfCalls"
    },
    message = "Legacy ingest and call-history endpoints were removed. Use OAuth2 token issuance, client/device/display configuration endpoints, kiosk command endpoints, and the Windows service local database/reporting pipeline."
})).RequireAuthorization();

api.MapPost("/ingest", async (HttpContext http, AppDbContext db, EmergencyCallUnified incident) =>
{
    if (incident is null)
        return Results.BadRequest(new { message = "Incident payload is required." });

    var incidentId = incident.GetCallIdentifier()?.Trim();
    if (string.IsNullOrWhiteSpace(incidentId))
        return Results.BadRequest(new { message = "Incident identifier is required. Supply details.id, id, num1, or c_num." });

    var apiClientIdValue = http.User.FindFirst("api_client_id")?.Value;
    if (!int.TryParse(apiClientIdValue, out var apiClientId) || apiClientId <= 0)
        return Results.Unauthorized();

    var client = await db.ApiClients.Include(x => x.SourceSystem).FirstOrDefaultAsync(x => x.Id == apiClientId);
    if (client is null || !client.IsEnabled)
        return Results.Unauthorized();

    var normalized = NormalizeIncidentForIngest(incident, client);
    var canonicalJson = EmergencyCallUnifiedJson.Serialize(normalized);
    var updatedAtUtc = normalized.GetUpdatedAtUtc() ?? normalized.GetCreatedAtUtc() ?? DateTime.UtcNow;
    var isClosed = normalized.GetIsClosed();
    var receivedAtUtc = DateTime.UtcNow;

    var existing = await db.IngestedIncidents.FirstOrDefaultAsync(x => x.ApiClientId == client.Id && x.IncidentId == incidentId);
    if (existing is null)
    {
        existing = new IngestedIncident
        {
            ApiClientId = client.Id,
            AgencyId = client.AgencyId,
            IncidentId = incidentId
        };
        db.IngestedIncidents.Add(existing);
    }

    existing.CanonicalJson = canonicalJson;
    existing.Status = normalized.Details?.Status;
    existing.IsClosed = isClosed;
    existing.Agency = normalized.GetAgencyName();
    existing.Address = normalized.GetAddress();
    existing.CallType = normalized.GetCallType();
    existing.UpdatedAtUtc = updatedAtUtc;
    existing.ReceivedAtUtc = receivedAtUtc;
    client.UpdatedAtUtc = receivedAtUtc;

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        message = existing.Id > 0 ? "Ingested" : "Accepted",
        incidentId,
        apiClientId = client.Id,
        clientId = client.ClientId,
        agencyId = client.AgencyId,
        isClosed,
        updatedAtUtc,
        receivedAtUtc
    });
})
.RequireAuthorization("scope:ingest")
.RequireRateLimiting("iar-ingest")
.Accepts<EmergencyCallUnified>("application/json")
.WithName("IngestIncident")
.WithSummary("Receive transmitted call data")
.WithDescription("Receives a unified emergency call payload and stores the latest canonical snapshot for the authenticated API client.");

api.MapGet("/service/incidents", async (AppDbContext db, ClaimsPrincipal user, int? take) =>
{
    var apiClientIdValue = user.FindFirst("api_client_id")?.Value;
    if (!int.TryParse(apiClientIdValue, out var apiClientId) || apiClientId <= 0)
        return Results.Unauthorized();

    var limit = Math.Clamp(take ?? 250, 1, 1000);
    var items = await db.IngestedIncidents
        .AsNoTracking()
        .Where(x => x.ApiClientId == apiClientId)
        .OrderByDescending(x => x.UpdatedAtUtc ?? x.ReceivedAtUtc)
        .ThenByDescending(x => x.Id)
        .Take(limit)
        .Select(x => x.CanonicalJson)
        .ToListAsync();

    var payload = items
        .Select(json => EmergencyCallUnifiedJson.Deserialize(json))
        .Where(x => x is not null)
        .Cast<EmergencyCallUnified>()
        .ToList();

    return Results.Ok(new
    {
        items = payload,
        count = payload.Count
    });
})
.RequireAuthorization("scope:service")
.WithName("GetServiceIncidents")
.WithSummary("Retrieve ingested incidents for the authenticated API client")
.WithDescription("Returns the latest stored incident snapshots for service polling.");
api.MapGet("/source-systems", async (AppDbContext db) =>
{
    var list = await db.SourceSystems.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    return Results.Ok(list);
}).RequireAuthorization("scope:clients.read");

api.MapGet("/clients", async (AppDbContext db) =>
{
    var list = await db.ApiClients.Include(x => x.SourceSystem).AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    return Results.Ok(list.Select(ToApiClientSummary));
}).RequireAuthorization("scope:clients.read");

api.MapGet("/clients/{id:int}", async (AppDbContext db, int id) =>
{
    var client = await db.ApiClients.Include(x => x.SourceSystem).FirstOrDefaultAsync(x => x.Id == id);
    return client is null ? Results.NotFound() : Results.Ok(ToApiClientDetail(client));
}).RequireAuthorization("scope:clients.read");

api.MapGet("/cutover/summary", async (AppDbContext db) =>
{
    var retiredTables = await DiscoverRetiredTablesAsync(db);
    var clients = await db.ApiClients.AsNoTracking().OrderBy(x => x.Id).ToListAsync();

    var clientSummary = clients.Select(client =>
    {
        var global = ParseGlobalSettingsDocument(client.GlobalSettingsJson);
        var devices = ParseDeviceSettings(client.DeviceSettingsJson);
        var displays = ParseDisplayRegistrations(client.DisplayRegistrationsJson);
        var commands = ParseDeviceCommands(client.DeviceCommandsJson);

        return new
        {
            client.Id,
            client.ClientId,
            client.Name,
            client.AgencyId,
            hasGlobalSettings = global.Settings.Count > 0,
            deviceCount = devices.Count,
            displayCount = displays.Count,
            pendingCommands = commands.Count(x => x.Status is DeviceCommandStatuses.Pending or DeviceCommandStatuses.Acknowledged or DeviceCommandStatuses.Running),
            secretMigrated = !string.IsNullOrWhiteSpace(client.ClientSecretHash) && !string.IsNullOrWhiteSpace(client.ClientSecretSalt),
            updatedAtUtc = client.UpdatedAtUtc
        };
    }).ToList();

    return Results.Ok(new
    {
        retiredTables,
        recommendedDropScript = BuildRetiredTableDropScript(retiredTables),
        clients = clientSummary,
        notes = new[]
        {
            "Review retiredTables before executing the drop step.",
            "Kiosk bootstrap data should exist as device settings plus display registrations before cutover.",
            "Service clients should have their service runtime metadata copied into global settings during migration."
        }
    });
}).RequireAuthorization("scope:clients.read");

api.MapPost("/cutover/apply", async (AppDbContext db, CutoverApplyRequest req) =>
{
    if (!req.DropRetiredTables)
        return Results.Ok(new { message = "No destructive action requested.", retiredTables = await DiscoverRetiredTablesAsync(db) });

    if (!req.Force)
        return Results.BadRequest(new { message = "Set Force=true to apply retired table drop statements after reviewing the summary endpoint." });

    var retiredTables = await DiscoverRetiredTablesAsync(db);
    if (retiredTables.Count == 0)
        return Results.Ok(new { message = "No retired tables were found.", retiredTables });

    if (!db.Database.IsSqlServer())
        return Results.BadRequest(new { message = "Retired table drop automation currently targets SQL Server only.", retiredTables });

    var script = BuildRetiredTableDropScript(retiredTables);
    if (!string.IsNullOrWhiteSpace(script))
        await db.Database.ExecuteSqlRawAsync(script);

    return Results.Ok(new
    {
        message = "Retired tables dropped.",
        retiredTables,
        reason = req.Reason
    });
}).RequireAuthorization("scope:clients.write");
api.MapPost("/clients", async (AppDbContext db, LookupUpsertService lookups, ApiClientUpsertRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.ClientId) || string.IsNullOrWhiteSpace(req.ClientSecret) || req.AgencyId <= 0)
        return Results.BadRequest(new { message = "AgencyId, ClientId, and ClientSecret are required." });

    if (await db.ApiClients.AnyAsync(x => x.ClientId == req.ClientId))
        return Results.Conflict(new { message = "ClientId already exists" });

    var sourceSystemId = await lookups.EnsureSourceSystemAsync(req.SourceSystemCode ?? "IAR");
    var client = new ApiClient
    {
        AgencyId = req.AgencyId,
        ClientId = req.ClientId,
        Name = req.Name ?? req.ClientId,
        IsEnabled = req.IsEnabled,
        SourceSystemId = sourceSystemId,
        AllowedScopesJson = JsonSerializer.Serialize(NormalizeScopes(req.AllowedScopes)),
        UpdatedAtUtc = DateTime.UtcNow
    };
    client.SetClientSecret(req.ClientSecret!);

    db.ApiClients.Add(client);
    await db.SaveChangesAsync();
    return Results.Created($"/api/clients/{client.Id}", new { id = client.Id });
}).RequireAuthorization("scope:clients.write");

api.MapPut("/clients/{id:int}", async (AppDbContext db, LookupUpsertService lookups, int id, ApiClientUpsertRequest req) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    client.AgencyId = req.AgencyId > 0 ? req.AgencyId : client.AgencyId;
    if (!string.IsNullOrWhiteSpace(req.ClientId)) client.ClientId = req.ClientId;
    if (!string.IsNullOrWhiteSpace(req.Name)) client.Name = req.Name;
    client.IsEnabled = req.IsEnabled;
    client.SourceSystemId = await lookups.EnsureSourceSystemAsync(req.SourceSystemCode ?? client.EffectiveSourceSystemCode);
    if (req.AllowedScopes is { Length: > 0 }) client.AllowedScopesJson = JsonSerializer.Serialize(NormalizeScopes(req.AllowedScopes));
    if (!string.IsNullOrWhiteSpace(req.ClientSecret)) client.SetClientSecret(req.ClientSecret);
    client.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Updated" });
}).RequireAuthorization("scope:clients.write");

api.MapPost("/clients/{id:int}/rotate-secret", async (AppDbContext db, int id, RotateClientSecretRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.NewClientSecret))
        return Results.BadRequest(new { message = "NewClientSecret is required." });

    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    client.SetClientSecret(req.NewClientSecret);
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        message = "Secret rotated",
        client.ClientSecretVersion,
        client.ClientSecretRotatedAtUtc
    });
}).RequireAuthorization("scope:clients.write");

api.MapGet("/clients/{id:int}/global-settings", async (AppDbContext db, int id) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    return Results.Ok(ParseGlobalSettingsDocument(client.GlobalSettingsJson));
}).RequireAuthorization("scope:global.read");

api.MapPut("/clients/{id:int}/global-settings", async (HttpContext http, AppDbContext db, int id, GlobalSettingsUpdateRequest req) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    client.GlobalSettingsJson = SerializeGlobalSettings(req.Settings, BuildAudit(req.Audit, http.User, "global-settings"));
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Updated", clientId = client.Id });
}).RequireAuthorization("scope:global.write");

api.MapPost("/clients/{id:int}/migrate/legacy-kiosk", async (HttpContext http, AppDbContext db, int id, LegacyKioskMigrationRequest req) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    var deviceId = string.IsNullOrWhiteSpace(req.DeviceId)
        ? $"legacy-{client.ClientId.ToLowerInvariant()}"
        : req.DeviceId.Trim();

    var audit = BuildAudit(req.Audit, http.User, "legacy-kiosk-migration");
    var global = ParseGlobalSettingsDocument(client.GlobalSettingsJson);
    var deviceSettings = ParseDeviceSettings(client.DeviceSettingsJson);
    var displays = ParseDisplayRegistrations(client.DisplayRegistrationsJson);

    if (!string.IsNullOrWhiteSpace(req.ApiBaseUrl))
        global.Settings["apiBaseUrl"] = req.ApiBaseUrl;

    if (req.PromoteStartupUrlToGlobalSettings && !string.IsNullOrWhiteSpace(req.StartupUrl))
        global.Settings["startupUrl"] = req.StartupUrl;

    global.Settings["migration"] = MergeObjects(global.Settings["migration"] as JsonObject, new JsonObject
    {
        ["legacyKioskMigratedAtUtc"] = DateTime.UtcNow,
        ["legacyKioskDeviceId"] = deviceId
    });

    var profile = new DeviceProfileDto
    {
        DisplayName = req.DisplayName,
        Location = req.Location,
        StationCode = req.StationCode,
        StationName = req.StationName,
        StartupUrl = req.StartupUrl,
        DisplaySource = req.DisplaySource ?? req.StartupUrl,
        DefaultPrinterName = req.DefaultPrinterName,
        Enabled = req.Enabled
    };

    var settings = new JsonObject();
    if (req.SelectedMonitorIndex.HasValue) settings["selectedMonitorIndex"] = req.SelectedMonitorIndex.Value;
    if (!string.IsNullOrWhiteSpace(req.StartupUrl)) settings["startupUrl"] = req.StartupUrl;
    if (!string.IsNullOrWhiteSpace(req.DisplaySource)) settings["displaySource"] = req.DisplaySource;
    if (!string.IsNullOrWhiteSpace(req.DefaultPrinterName)) settings["defaultPrinterName"] = req.DefaultPrinterName;

    var deviceEntry = UpsertDeviceSettingsEntry(deviceSettings, deviceId, new DeviceSettingsUpsertRequest
    {
        DeviceId = deviceId,
        Profile = profile,
        Settings = settings,
        Audit = req.Audit
    }, audit);

    var displayEntry = UpsertDisplayRegistration(displays, deviceId, new DisplayRegistrationUpsertRequest
    {
        DeviceId = deviceId,
        Name = req.DisplayName,
        Location = req.Location,
        Enabled = req.Enabled,
        Settings = settings,
        Profile = profile,
        Audit = req.Audit
    }, audit);

    client.GlobalSettingsJson = JsonSerializer.Serialize(global, JsonUtil.Options);
    client.DeviceSettingsJson = JsonSerializer.Serialize(deviceSettings, JsonUtil.Options);
    client.DisplayRegistrationsJson = JsonSerializer.Serialize(displays, JsonUtil.Options);
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        message = "Legacy kiosk settings migrated.",
        device = deviceEntry,
        display = displayEntry,
        configuration = ResolveDeviceConfiguration(client, deviceId)
    });
}).RequireAuthorization("scope:device.write");

api.MapPost("/clients/{id:int}/migrate/legacy-service", async (HttpContext http, AppDbContext db, int id, LegacyServiceMigrationRequest req) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    var audit = BuildAudit(req.Audit, http.User, "legacy-service-migration");
    var global = ParseGlobalSettingsDocument(client.GlobalSettingsJson);
    var serviceNode = MergeObjects(global.Settings["service"] as JsonObject, new JsonObject());

    SetIfPresent(serviceNode, "apiBaseUrl", req.ApiBaseUrl);
    SetIfPresent(serviceNode, "incidentFeedPath", req.IncidentFeedPath);
    SetIfPresent(serviceNode, "oauthTokenPath", req.OAuthTokenPath);
    SetIfPresent(serviceNode, "clientId", req.ClientId);
    SetIfPresent(serviceNode, "scope", req.Scope);
    SetIfPresent(serviceNode, "localDataDirectory", req.LocalDataDirectory);
    SetIfPresent(serviceNode, "databaseFileName", req.DatabaseFileName);
    SetIfPresent(serviceNode, "printOutputDirectory", req.PrintOutputDirectory);
    SetIfPresent(serviceNode, "healthLogDirectory", req.HealthLogDirectory);
    SetIfPresent(serviceNode, "printerName", req.PrinterName);
    if (req.PollIntervalSeconds.HasValue) serviceNode["pollIntervalSeconds"] = req.PollIntervalSeconds.Value;
    if (req.HttpTimeoutSeconds.HasValue) serviceNode["httpTimeoutSeconds"] = req.HttpTimeoutSeconds.Value;
    if (req.MaxConsecutiveFailuresBeforeBackoff.HasValue) serviceNode["maxConsecutiveFailuresBeforeBackoff"] = req.MaxConsecutiveFailuresBeforeBackoff.Value;
    if (req.MaxBackoffMinutes.HasValue) serviceNode["maxBackoffMinutes"] = req.MaxBackoffMinutes.Value;
    if (req.MaxPrintAttempts.HasValue) serviceNode["maxPrintAttempts"] = req.MaxPrintAttempts.Value;
    if (req.EnableShellPrinting.HasValue) serviceNode["enableShellPrinting"] = req.EnableShellPrinting.Value;
    if (req.Metadata is not null) serviceNode["metadata"] = req.Metadata.DeepClone();
    serviceNode["migratedAtUtc"] = DateTime.UtcNow;

    global.Settings["service"] = serviceNode;
    global.Settings["migration"] = MergeObjects(global.Settings["migration"] as JsonObject, new JsonObject
    {
        ["legacyServiceMigratedAtUtc"] = DateTime.UtcNow
    });

    client.GlobalSettingsJson = SerializeGlobalSettings(global.Settings, audit);
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        message = "Legacy service settings migrated.",
        globalSettings = ParseGlobalSettingsDocument(client.GlobalSettingsJson)
    });
}).RequireAuthorization("scope:global.write");

api.MapGet("/clients/{id:int}/device-settings", async (AppDbContext db, int id) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    return Results.Ok(ParseDeviceSettings(client.DeviceSettingsJson));
}).RequireAuthorization("scope:device.read");

api.MapGet("/clients/{id:int}/device-settings/{deviceId}", async (AppDbContext db, int id, string deviceId) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    var existing = ParseDeviceSettings(client.DeviceSettingsJson).FirstOrDefault(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    return existing is null ? Results.NotFound() : Results.Ok(existing);
}).RequireAuthorization("scope:device.read");

api.MapPut("/clients/{id:int}/device-settings/{deviceId}", async (HttpContext http, AppDbContext db, int id, string deviceId, DeviceSettingsUpsertRequest req) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(deviceId)) return Results.BadRequest(new { message = "DeviceId is required." });

    var list = ParseDeviceSettings(client.DeviceSettingsJson);
    var entry = UpsertDeviceSettingsEntry(list, deviceId, req, BuildAudit(req.Audit, http.User, "device-settings"));

    client.DeviceSettingsJson = JsonSerializer.Serialize(list, JsonUtil.Options);
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(entry);
}).RequireAuthorization("scope:device.write");

api.MapGet("/clients/{id:int}/displays", async (AppDbContext db, int id) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    return Results.Ok(ParseDisplayRegistrations(client.DisplayRegistrationsJson));
}).RequireAuthorization("scope:display.read");

api.MapGet("/clients/{id:int}/displays/{deviceId}", async (AppDbContext db, int id, string deviceId) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    var existing = ParseDisplayRegistrations(client.DisplayRegistrationsJson).FirstOrDefault(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    return existing is null ? Results.NotFound() : Results.Ok(existing);
}).RequireAuthorization("scope:display.read");

api.MapPost("/clients/{id:int}/displays", async (HttpContext http, AppDbContext db, int id, DisplayRegistrationUpsertRequest req) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(req.DeviceId)) return Results.BadRequest(new { message = "DeviceId is required." });

    var list = ParseDisplayRegistrations(client.DisplayRegistrationsJson);
    if (list.Any(x => string.Equals(x.DeviceId, req.DeviceId, StringComparison.OrdinalIgnoreCase)))
        return Results.Conflict(new { message = "Display registration already exists." });

    var entry = ToDisplayRegistration(req, BuildAudit(req.Audit, http.User, "display-registration"));
    list.Add(entry);
    client.DisplayRegistrationsJson = JsonSerializer.Serialize(list, JsonUtil.Options);
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Created($"/api/clients/{id}/displays/{req.DeviceId}", entry);
}).RequireAuthorization("scope:display.write");

api.MapPut("/clients/{id:int}/displays/{deviceId}", async (HttpContext http, AppDbContext db, int id, string deviceId, DisplayRegistrationUpsertRequest req) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    var list = ParseDisplayRegistrations(client.DisplayRegistrationsJson);
    var entry = UpsertDisplayRegistration(list, deviceId, req, BuildAudit(req.Audit, http.User, "display-registration"));

    client.DisplayRegistrationsJson = JsonSerializer.Serialize(list, JsonUtil.Options);
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(entry);
}).RequireAuthorization("scope:display.write");

api.MapGet("/clients/{id:int}/devices/{deviceId}/resolved-config", async (AppDbContext db, int id, string deviceId) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    var resolved = ResolveDeviceConfiguration(client, deviceId);
    return resolved is null ? Results.NotFound() : Results.Ok(resolved);
}).RequireAuthorization("scope:display.read");

api.MapGet("/me", async (HttpContext http, AppDbContext db) =>
{
    var client = await GetAuthorizedApiClientAsync(db, http.User);
    return client is null ? Results.Unauthorized() : Results.Ok(ToApiClientDetail(client));
}).RequireAuthorization();

api.MapGet("/me/global-settings", async (HttpContext http, AppDbContext db) =>
{
    var client = await GetAuthorizedApiClientAsync(db, http.User);
    return client is null ? Results.Unauthorized() : Results.Ok(ParseGlobalSettingsDocument(client.GlobalSettingsJson));
}).RequireAuthorization("scope:global.read");

api.MapGet("/me/devices", async (HttpContext http, AppDbContext db) =>
{
    var client = await GetAuthorizedApiClientAsync(db, http.User);
    if (client is null) return Results.Unauthorized();

    var devices = ParseDeviceSettings(client.DeviceSettingsJson)
        .Select(x => ResolveDeviceConfiguration(client, x.DeviceId))
        .Where(x => x is not null)
        .ToList();

    return Results.Ok(devices);
}).RequireAuthorization("scope:device.read");

api.MapPost("/me/devices/register", async (HttpContext http, AppDbContext db, DeviceBootstrapRequest req) =>
{
    var client = await GetAuthorizedApiClientAsync(db, http.User);
    if (client is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(req.DeviceId)) return Results.BadRequest(new { message = "DeviceId is required." });

    var audit = BuildAudit(req.Audit, http.User, "device-bootstrap");
    var deviceSettings = ParseDeviceSettings(client.DeviceSettingsJson);
    var displayRegistrations = ParseDisplayRegistrations(client.DisplayRegistrationsJson);

    var upsertRequest = new DeviceSettingsUpsertRequest
    {
        DeviceId = req.DeviceId,
        Profile = req.Profile,
        Settings = req.Settings ?? new JsonObject(),
        Audit = req.Audit
    };

    var entry = UpsertDeviceSettingsEntry(deviceSettings, req.DeviceId, upsertRequest, audit);

    if (req.UpsertRegistration)
    {
        UpsertDisplayRegistration(displayRegistrations, req.DeviceId, new DisplayRegistrationUpsertRequest
        {
            DeviceId = req.DeviceId,
            Name = req.Profile?.DisplayName,
            Description = req.Profile?.Description,
            Location = req.Profile?.Location,
            Enabled = req.Profile?.Enabled ?? true,
            Settings = req.Settings ?? new JsonObject(),
            Profile = req.Profile,
            Audit = req.Audit
        }, audit);
    }

    client.DeviceSettingsJson = JsonSerializer.Serialize(deviceSettings, JsonUtil.Options);
    client.DisplayRegistrationsJson = JsonSerializer.Serialize(displayRegistrations, JsonUtil.Options);
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    var resolved = ResolveDeviceConfiguration(client, req.DeviceId);
    return Results.Ok(new
    {
        message = "Device registered",
        device = entry,
        configuration = resolved
    });
}).RequireAuthorization("scope:device.write");

api.MapGet("/me/devices/{deviceId}/configuration", async (HttpContext http, AppDbContext db, string deviceId) =>
{
    var client = await GetAuthorizedApiClientAsync(db, http.User);
    if (client is null) return Results.Unauthorized();

    var resolved = ResolveDeviceConfiguration(client, deviceId);
    return resolved is null ? Results.NotFound() : Results.Ok(resolved);
}).RequireAuthorization("scope:display.read");

api.MapGet("/clients/{id:int}/commands", async (AppDbContext db, int id, string? deviceId) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();

    var commands = ParseDeviceCommands(client.DeviceCommandsJson);
    if (!string.IsNullOrWhiteSpace(deviceId))
        commands = commands.Where(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)).ToList();

    commands = commands
        .OrderByDescending(x => x.IssuedAtUtc)
        .Take(100)
        .ToList();

    return Results.Ok(commands);
}).RequireAuthorization("scope:kiosk.commands");

api.MapPost("/clients/{id:int}/commands", async (HttpContext http, AppDbContext db, int id, DeviceCommandIssueRequest req) =>
{
    var client = await db.ApiClients.FirstOrDefaultAsync(x => x.Id == id);
    if (client is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(req.DeviceId)) return Results.BadRequest(new { message = "DeviceId is required." });

    var commands = ParseDeviceCommands(client.DeviceCommandsJson);
    var audit = BuildAudit(req.Audit, http.User, "device-command");
    var command = CreateCommand(req, audit);
    commands.Add(command);

    client.DeviceCommandsJson = JsonSerializer.Serialize(commands, JsonUtil.Options);
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(command);
}).RequireAuthorization("scope:kiosk.commands");

api.MapPost("/kiosk/commands/{command}", async (HttpContext http, AppDbContext db, string command, string? deviceId) =>
{
    var client = await GetAuthorizedApiClientAsync(db, http.User);
    if (client is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(deviceId)) return Results.BadRequest(new { message = "deviceId is required." });

    var commands = ParseDeviceCommands(client.DeviceCommandsJson);
    var audit = BuildAudit(null, http.User, "device-command");
    var entry = CreateCommand(new DeviceCommandIssueRequest
    {
        DeviceId = deviceId!,
        Command = command,
        TimeoutSeconds = 300
    }, audit);

    commands.Add(entry);
    client.DeviceCommandsJson = JsonSerializer.Serialize(commands, JsonUtil.Options);
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(entry);
}).RequireAuthorization("scope:kiosk.commands");

api.MapGet("/me/devices/{deviceId}/commands/pending", async (HttpContext http, AppDbContext db, string deviceId) =>
{
    var client = await GetAuthorizedApiClientAsync(db, http.User);
    if (client is null) return Results.Unauthorized();

    var commands = ParseDeviceCommands(client.DeviceCommandsJson);
    MarkStaleCommands(commands);

    var active = commands
        .Where(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
        .Where(x => x.Status is DeviceCommandStatuses.Pending or DeviceCommandStatuses.Acknowledged or DeviceCommandStatuses.Running)
        .OrderBy(x => x.IssuedAtUtc)
        .ToList();

    client.DeviceCommandsJson = JsonSerializer.Serialize(commands, JsonUtil.Options);
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(active);
}).RequireAuthorization("scope:kiosk.commands");

api.MapPost("/me/devices/{deviceId}/commands/{commandId}/heartbeat", async (HttpContext http, AppDbContext db, string deviceId, string commandId, DeviceHeartbeatRequest req) =>
{
    var client = await GetAuthorizedApiClientAsync(db, http.User);
    if (client is null) return Results.Unauthorized();

    var commands = ParseDeviceCommands(client.DeviceCommandsJson);
    var entry = commands.FirstOrDefault(x => x.Id == commandId && string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    if (entry is null) return Results.NotFound();

    entry.LastHeartbeatAtUtc = DateTime.UtcNow;
    if (!string.IsNullOrWhiteSpace(req.Status))
        entry.Status = NormalizeCommandStatus(req.Status);
    if (!string.IsNullOrWhiteSpace(req.Message))
        entry.ResultMessage = req.Message;
    if (req.Details is not null)
        entry.Details = req.Details;
    if (entry.Status == DeviceCommandStatuses.Running && entry.StartedAtUtc is null)
        entry.StartedAtUtc = DateTime.UtcNow;

    client.DeviceCommandsJson = JsonSerializer.Serialize(commands, JsonUtil.Options);
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(entry);
}).RequireAuthorization("scope:kiosk.commands");

api.MapPost("/me/devices/{deviceId}/commands/{commandId}/ack", async (HttpContext http, AppDbContext db, string deviceId, string commandId, DeviceCommandAckRequest req) =>
{
    var client = await GetAuthorizedApiClientAsync(db, http.User);
    if (client is null) return Results.Unauthorized();

    var commands = ParseDeviceCommands(client.DeviceCommandsJson);
    var entry = commands.FirstOrDefault(x => x.Id == commandId && string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    if (entry is null) return Results.NotFound();

    entry.AcknowledgedAtUtc ??= DateTime.UtcNow;
    entry.LastHeartbeatAtUtc = DateTime.UtcNow;
    entry.Status = NormalizeCommandStatus(req.Status);
    if (entry.Status == DeviceCommandStatuses.Running)
        entry.StartedAtUtc ??= DateTime.UtcNow;
    if (entry.Status is DeviceCommandStatuses.Succeeded or DeviceCommandStatuses.Failed or DeviceCommandStatuses.Stale)
        entry.CompletedAtUtc ??= DateTime.UtcNow;
    entry.ResultMessage = req.Message;
    if (req.Details is not null)
        entry.Details = req.Details;

    client.DeviceCommandsJson = JsonSerializer.Serialize(commands, JsonUtil.Options);
    client.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(entry);
}).RequireAuthorization("scope:kiosk.commands");

app.Run();

static async Task EnsureEpic5SchemaAsync(AppDbContext db)
{
    if (!db.Database.IsSqlServer())
        return;

    var sql = @"
IF COL_LENGTH('ApiClients', 'AllowedScopesJson') IS NULL
    ALTER TABLE ApiClients ADD AllowedScopesJson nvarchar(max) NOT NULL CONSTRAINT DF_ApiClients_AllowedScopesJson DEFAULT '[]';

IF COL_LENGTH('ApiClients', 'DeviceCommandsJson') IS NULL
    ALTER TABLE ApiClients ADD DeviceCommandsJson nvarchar(max) NOT NULL CONSTRAINT DF_ApiClients_DeviceCommandsJson DEFAULT '[]';

IF COL_LENGTH('ApiClients', 'ClientSecretHash') IS NULL
    ALTER TABLE ApiClients ADD ClientSecretHash nvarchar(256) NOT NULL CONSTRAINT DF_ApiClients_ClientSecretHash DEFAULT '';

IF COL_LENGTH('ApiClients', 'ClientSecretSalt') IS NULL
    ALTER TABLE ApiClients ADD ClientSecretSalt nvarchar(256) NOT NULL CONSTRAINT DF_ApiClients_ClientSecretSalt DEFAULT '';

IF COL_LENGTH('ApiClients', 'ClientSecretVersion') IS NULL
    ALTER TABLE ApiClients ADD ClientSecretVersion int NOT NULL CONSTRAINT DF_ApiClients_ClientSecretVersion DEFAULT 1;

IF COL_LENGTH('ApiClients', 'ClientSecretRotatedAtUtc') IS NULL
    ALTER TABLE ApiClients ADD ClientSecretRotatedAtUtc datetime2 NULL;

IF COL_LENGTH('ApiClients', 'ClientSecret') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE ApiClients
        SET ClientSecretRotatedAtUtc = ISNULL(ClientSecretRotatedAtUtc, UpdatedAtUtc)
        WHERE ClientSecret IS NOT NULL
          AND ClientSecret <> ''''
          AND (ClientSecretHash = '''' OR ClientSecretSalt = '''');
    ';
END
";

    await db.Database.ExecuteSqlRawAsync(sql);

    var clientsNeedingDefaults = await db.ApiClients
        .Where(x => (x.ClientSecretHash == "" || x.ClientSecretSalt == "")
                    || string.IsNullOrWhiteSpace(x.AllowedScopesJson)
                    || x.AllowedScopesJson == "{}")
        .ToListAsync();

    foreach (var client in clientsNeedingDefaults)
    {
        if (string.IsNullOrWhiteSpace(client.AllowedScopesJson) || client.AllowedScopesJson == "{}")
            client.AllowedScopesJson = JsonSerializer.Serialize(ScopeCatalog.DefaultClientScopes);
    }

    if (clientsNeedingDefaults.Count > 0)
        await db.SaveChangesAsync();
}
static async Task<string?> TryReadLegacyClientSecretAsync(AppDbContext db, int clientId)
{
    var conn = db.Database.GetDbConnection();
    var shouldClose = conn.State != System.Data.ConnectionState.Open;
    if (shouldClose) await conn.OpenAsync();
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TOP 1 ClientSecret FROM ApiClients WHERE Id = @id";
        var p = cmd.CreateParameter();
        p.ParameterName = "@id";
        p.Value = clientId;
        cmd.Parameters.Add(p);
        var value = await cmd.ExecuteScalarAsync();
        return value == DBNull.Value || value is null ? null : Convert.ToString(value);
    }
    catch
    {
        return null;
    }
    finally
    {
        if (shouldClose) await conn.CloseAsync();
    }
}
static async Task<(bool Ok, string Body, string? Error)> ReadBodySafeAsync(HttpRequest request, int maxBytes)
{
    request.EnableBuffering();
    using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
    var body = await reader.ReadToEndAsync();
    request.Body.Position = 0;

    if (Encoding.UTF8.GetByteCount(body) > maxBytes)
        return (false, string.Empty, $"Request body exceeds {maxBytes} bytes.");

    return (true, body, null);
}

static string StableHash(string value) => Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)))
    .Replace("=", string.Empty)
    .Replace("+", "-")
    .Replace("/", "_")
    .Substring(0, 22);

static object ToApiClientSummary(ApiClient client) => new
{
    client.Id,
    client.AgencyId,
    client.ClientId,
    client.Name,
    client.IsEnabled,
    sourceSystem = client.EffectiveSourceSystemCode,
    allowedScopes = client.AllowedScopes,
    client.ClientSecretVersion,
    client.ClientSecretRotatedAtUtc,
    client.CreatedAtUtc,
    client.UpdatedAtUtc
};

static object ToApiClientDetail(ApiClient client) => new
{
    client.Id,
    client.AgencyId,
    client.ClientId,
    client.Name,
    client.IsEnabled,
    sourceSystem = client.EffectiveSourceSystemCode,
    allowedScopes = client.AllowedScopes,
    globalSettings = ParseGlobalSettingsDocument(client.GlobalSettingsJson),
    deviceSettings = ParseDeviceSettings(client.DeviceSettingsJson),
    displayRegistrations = ParseDisplayRegistrations(client.DisplayRegistrationsJson),
    deviceCommands = ParseDeviceCommands(client.DeviceCommandsJson),
    client.ClientSecretVersion,
    client.ClientSecretRotatedAtUtc,
    client.CreatedAtUtc,
    client.UpdatedAtUtc
};

static string NormalizeJsonObject(string? json)
{
    var node = ParseJsonNode(json);
    return JsonSerializer.Serialize(node as JsonObject ?? new JsonObject(), JsonUtil.Options);
}

static string NormalizeJsonArray(string? json)
{
    var node = ParseJsonNode(json);
    return JsonSerializer.Serialize(node as JsonArray ?? new JsonArray(), JsonUtil.Options);
}

static JsonNode? ParseJsonNode(string? json)
{
    if (string.IsNullOrWhiteSpace(json)) return null;
    try { return JsonNode.Parse(json); }
    catch { return null; }
}

static GlobalSettingsDocument ParseGlobalSettingsDocument(string? json)
{
    if (string.IsNullOrWhiteSpace(json))
        return new GlobalSettingsDocument();

    try
    {
        var parsed = JsonSerializer.Deserialize<GlobalSettingsDocument>(json, JsonUtil.Options);
        if (parsed is not null && parsed.Settings is not null)
            return parsed;
    }
    catch
    {
    }

    var legacy = ParseJsonNode(json) as JsonObject;
    return new GlobalSettingsDocument { Settings = legacy ?? new JsonObject() };
}

static string SerializeGlobalSettings(JsonObject? settings, AuditMetadata? audit)
    => JsonSerializer.Serialize(new GlobalSettingsDocument
    {
        Settings = settings ?? new JsonObject(),
        Audit = audit,
        UpdatedAtUtc = DateTime.UtcNow
    }, JsonUtil.Options);

static List<DeviceSettingsEntry> ParseDeviceSettings(string? json)
{
    if (string.IsNullOrWhiteSpace(json)) return new();
    try
    {
        return JsonSerializer.Deserialize<List<DeviceSettingsEntry>>(json, JsonUtil.Options) ?? new();
    }
    catch
    {
        return new();
    }
}

static List<DisplayRegistrationEntry> ParseDisplayRegistrations(string? json)
{
    if (string.IsNullOrWhiteSpace(json)) return new();
    try
    {
        return JsonSerializer.Deserialize<List<DisplayRegistrationEntry>>(json, JsonUtil.Options) ?? new();
    }
    catch
    {
        return new();
    }
}

static DeviceSettingsEntry UpsertDeviceSettingsEntry(List<DeviceSettingsEntry> list, string deviceId, DeviceSettingsUpsertRequest req, AuditMetadata audit)
{
    var existing = list.FirstOrDefault(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    if (existing is null)
    {
        existing = new DeviceSettingsEntry
        {
            DeviceId = deviceId,
            Profile = NormalizeProfile(req.Profile),
            Settings = req.Settings ?? new JsonObject(),
            Audit = audit,
            UpdatedAtUtc = DateTime.UtcNow
        };
        list.Add(existing);
        return existing;
    }

    existing.Profile = MergeProfile(existing.Profile, req.Profile);
    existing.Settings = req.Settings ?? new JsonObject();
    existing.Audit = audit;
    existing.UpdatedAtUtc = DateTime.UtcNow;
    return existing;
}

static DisplayRegistrationEntry UpsertDisplayRegistration(List<DisplayRegistrationEntry> list, string deviceId, DisplayRegistrationUpsertRequest req, AuditMetadata audit)
{
    var existing = list.FirstOrDefault(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    if (existing is null)
    {
        existing = ToDisplayRegistration(req, audit);
        existing.DeviceId = deviceId;
        list.Add(existing);
        return existing;
    }

    existing.Name = req.Name ?? existing.Name ?? req.Profile?.DisplayName;
    existing.Description = req.Description ?? existing.Description ?? req.Profile?.Description;
    existing.Location = req.Location ?? existing.Location ?? req.Profile?.Location;
    existing.Enabled = req.Enabled;
    existing.Settings = req.Settings ?? new JsonObject();
    existing.Profile = MergeProfile(existing.Profile, req.Profile);
    existing.Audit = audit;
    existing.UpdatedAtUtc = DateTime.UtcNow;
    return existing;
}

static DisplayRegistrationEntry ToDisplayRegistration(DisplayRegistrationUpsertRequest req, AuditMetadata audit) => new()
{
    DeviceId = req.DeviceId,
    Name = req.Name ?? req.Profile?.DisplayName,
    Description = req.Description ?? req.Profile?.Description,
    Location = req.Location ?? req.Profile?.Location,
    Enabled = req.Enabled,
    Settings = req.Settings ?? new JsonObject(),
    Profile = NormalizeProfile(req.Profile),
    Audit = audit,
    UpdatedAtUtc = DateTime.UtcNow
};

static DeviceProfile? NormalizeProfile(DeviceProfileDto? dto)
{
    if (dto is null) return null;
    return new DeviceProfile
    {
        DisplayName = dto.DisplayName,
        Description = dto.Description,
        Location = dto.Location,
        StationCode = dto.StationCode,
        StationName = dto.StationName,
        StartupUrl = dto.StartupUrl,
        DisplaySource = dto.DisplaySource,
        RefreshSeconds = dto.RefreshSeconds,
        PrinterRouting = dto.PrinterRouting,
        DefaultPrinterName = dto.DefaultPrinterName,
        CommandState = dto.CommandState,
        Enabled = dto.Enabled,
        Metadata = dto.Metadata ?? new JsonObject()
    };
}

static DeviceProfile? MergeProfile(DeviceProfile? existing, DeviceProfileDto? incoming)
{
    if (incoming is null) return existing;
    var normalized = NormalizeProfile(incoming)!;
    if (existing is null) return normalized;

    existing.DisplayName = normalized.DisplayName ?? existing.DisplayName;
    existing.Description = normalized.Description ?? existing.Description;
    existing.Location = normalized.Location ?? existing.Location;
    existing.StationCode = normalized.StationCode ?? existing.StationCode;
    existing.StationName = normalized.StationName ?? existing.StationName;
    existing.StartupUrl = normalized.StartupUrl ?? existing.StartupUrl;
    existing.DisplaySource = normalized.DisplaySource ?? existing.DisplaySource;
    existing.RefreshSeconds = normalized.RefreshSeconds ?? existing.RefreshSeconds;
    existing.PrinterRouting = normalized.PrinterRouting ?? existing.PrinterRouting;
    existing.DefaultPrinterName = normalized.DefaultPrinterName ?? existing.DefaultPrinterName;
    existing.CommandState = normalized.CommandState ?? existing.CommandState;
    existing.Enabled = normalized.Enabled;
    existing.Metadata = normalized.Metadata?.DeepClone() as JsonObject ?? existing.Metadata ?? new JsonObject();
    return existing;
}

static DeviceResolvedConfiguration? ResolveDeviceConfiguration(ApiClient client, string deviceId)
{
    var deviceSettings = ParseDeviceSettings(client.DeviceSettingsJson).FirstOrDefault(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    var displayRegistration = ParseDisplayRegistrations(client.DisplayRegistrationsJson).FirstOrDefault(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    if (deviceSettings is null && displayRegistration is null)
        return null;

    var global = ParseGlobalSettingsDocument(client.GlobalSettingsJson);
    var profile = MergeProfile(displayRegistration?.Profile, null);
    profile = MergeProfile(profile, ToProfileDto(deviceSettings?.Profile));
    if (profile is null && displayRegistration is not null)
    {
        profile = new DeviceProfile
        {
            DisplayName = displayRegistration.Name,
            Description = displayRegistration.Description,
            Location = displayRegistration.Location,
            Enabled = displayRegistration.Enabled,
            Metadata = new JsonObject()
        };
    }

    if (profile is not null && displayRegistration is not null)
    {
        profile.DisplayName ??= displayRegistration.Name;
        profile.Description ??= displayRegistration.Description;
        profile.Location ??= displayRegistration.Location;
        profile.Enabled = displayRegistration.Enabled;
    }

    return new DeviceResolvedConfiguration
    {
        ClientId = client.ClientId,
        ApiClientId = client.Id,
        AgencyId = client.AgencyId,
        DeviceId = deviceId,
        Profile = profile,
        GlobalSettings = global.Settings,
        DeviceSettings = deviceSettings?.Settings ?? displayRegistration?.Settings ?? new JsonObject(),
        GlobalSettingsAudit = global.Audit,
        DeviceAudit = deviceSettings?.Audit,
        DisplayAudit = displayRegistration?.Audit,
        DeviceUpdatedAtUtc = deviceSettings?.UpdatedAtUtc,
        DisplayUpdatedAtUtc = displayRegistration?.UpdatedAtUtc
    };
}

static DeviceProfileDto? ToProfileDto(DeviceProfile? profile)
{
    if (profile is null) return null;
    return new DeviceProfileDto
    {
        DisplayName = profile.DisplayName,
        Description = profile.Description,
        Location = profile.Location,
        StationCode = profile.StationCode,
        StationName = profile.StationName,
        StartupUrl = profile.StartupUrl,
        DisplaySource = profile.DisplaySource,
        RefreshSeconds = profile.RefreshSeconds,
        PrinterRouting = profile.PrinterRouting,
        DefaultPrinterName = profile.DefaultPrinterName,
        CommandState = profile.CommandState,
        Enabled = profile.Enabled,
        Metadata = profile.Metadata?.DeepClone() as JsonObject ?? new JsonObject()
    };
}

static AuditMetadata BuildAudit(AuditStampDto? dto, ClaimsPrincipal user, string fallbackSource)
{
    var now = DateTime.UtcNow;
    return new AuditMetadata
    {
        ChangedAtUtc = now,
        ChangedBy = dto?.ChangedBy ?? user.FindFirst("client_id")?.Value ?? user.Identity?.Name ?? "unknown",
        Reason = dto?.Reason,
        Source = dto?.Source ?? fallbackSource
    };
}

static async Task<ApiClient?> GetAuthorizedApiClientAsync(AppDbContext db, ClaimsPrincipal user)
{
    var clientIdClaim = user.FindFirst("api_client_id")?.Value;
    if (int.TryParse(clientIdClaim, out var apiClientId))
        return await db.ApiClients.FirstOrDefaultAsync(x => x.Id == apiClientId && x.IsEnabled);

    var logicalClientId = user.FindFirst("client_id")?.Value;
    if (!string.IsNullOrWhiteSpace(logicalClientId))
        return await db.ApiClients.FirstOrDefaultAsync(x => x.ClientId == logicalClientId && x.IsEnabled);

    return null;
}

static bool HasAnyScope(ClaimsPrincipal user, params string[] requiredScopes)
{
    var userScopes = user.Claims
        .Where(c => c.Type == "scope")
        .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    return requiredScopes.Any(userScopes.Contains);
}

static IReadOnlyList<string> NormalizeScopes(IEnumerable<string>? scopes)
{
    var normalized = (scopes ?? ScopeCatalog.DefaultClientScopes)
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => s.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Where(ScopeCatalog.All.Contains)
        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return normalized.Length == 0 ? ScopeCatalog.DefaultClientScopes : normalized;
}

static List<string> ParseScopes(string? scope) => string.IsNullOrWhiteSpace(scope)
    ? new List<string>()
    : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

static List<DeviceCommandEntry> ParseDeviceCommands(string? json)
{
    try
    {
        return JsonSerializer.Deserialize<List<DeviceCommandEntry>>(string.IsNullOrWhiteSpace(json) ? "[]" : json, JsonUtil.Options) ?? new();
    }
    catch
    {
        return new();
    }
}

static DeviceCommandEntry CreateCommand(DeviceCommandIssueRequest req, AuditMetadata audit)
{
    var normalized = NormalizeCommandName(req.Command);
    return new DeviceCommandEntry
    {
        Id = Guid.NewGuid().ToString("N"),
        DeviceId = req.DeviceId.Trim(),
        Command = normalized,
        Status = DeviceCommandStatuses.Pending,
        IssuedAtUtc = DateTime.UtcNow,
        ExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Clamp(req.TimeoutSeconds ?? 300, 30, 86400)),
        Audit = audit,
        Details = req.Payload ?? new JsonObject()
    };
}

static void MarkStaleCommands(List<DeviceCommandEntry> commands)
{
    var now = DateTime.UtcNow;
    foreach (var command in commands)
    {
        if (command.Status is DeviceCommandStatuses.Succeeded or DeviceCommandStatuses.Failed or DeviceCommandStatuses.Stale)
            continue;

        if (command.ExpiresAtUtc.HasValue && command.ExpiresAtUtc.Value <= now)
        {
            command.Status = DeviceCommandStatuses.Stale;
            command.CompletedAtUtc ??= now;
            command.ResultMessage ??= "Command timed out before completion.";
        }
    }
}

static string NormalizeCommandName(string? command)
{
    var normalized = (command ?? string.Empty).Trim().ToLowerInvariant();
    return normalized switch
    {
        "refresh" => "refresh",
        "reload" => "reload",
        "reloadconfig" => "reloadconfig",
        "relogin" => "relogin",
        "clearcookies" => "clearcookies",
        "start" => "start",
        "stop" => "stop",
        "shutdown" => "shutdown",
        "restart" => "restart",
        _ => normalized
    };
}

static string NormalizeCommandStatus(string? status)
{
    var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
    return normalized switch
    {
        DeviceCommandStatuses.Pending => DeviceCommandStatuses.Pending,
        DeviceCommandStatuses.Acknowledged => DeviceCommandStatuses.Acknowledged,
        DeviceCommandStatuses.Running => DeviceCommandStatuses.Running,
        DeviceCommandStatuses.Succeeded => DeviceCommandStatuses.Succeeded,
        DeviceCommandStatuses.Failed => DeviceCommandStatuses.Failed,
        DeviceCommandStatuses.Stale => DeviceCommandStatuses.Stale,
        _ => DeviceCommandStatuses.Acknowledged
    };
}

static async Task EnsureEpic10SchemaAsync(AppDbContext db)
{
    if (!db.Database.IsSqlServer())
        return;

    var sql = @"
IF COL_LENGTH('ApiClients', 'DeviceCommandsJson') IS NULL
    ALTER TABLE ApiClients ADD DeviceCommandsJson nvarchar(max) NOT NULL CONSTRAINT DF_ApiClients_DeviceCommandsJson DEFAULT '[]';";

    await db.Database.ExecuteSqlRawAsync(sql);

    var missing = await db.ApiClients.Where(x => string.IsNullOrWhiteSpace(x.DeviceCommandsJson)).ToListAsync();
    if (missing.Count == 0)
        return;

    foreach (var client in missing)
        client.DeviceCommandsJson = "[]";

    await db.SaveChangesAsync();
}

static async Task EnsureEpic13SchemaAsync(AppDbContext db)
{
    var clients = await db.ApiClients.ToListAsync();
    var changed = false;
    foreach (var client in clients)
    {
        var normalizedGlobal = JsonSerializer.Serialize(ParseGlobalSettingsDocument(client.GlobalSettingsJson), JsonUtil.Options);
        var normalizedDevices = JsonSerializer.Serialize(ParseDeviceSettings(client.DeviceSettingsJson), JsonUtil.Options);
        var normalizedDisplays = JsonSerializer.Serialize(ParseDisplayRegistrations(client.DisplayRegistrationsJson), JsonUtil.Options);

        if (!string.Equals(client.GlobalSettingsJson, normalizedGlobal, StringComparison.Ordinal))
        {
            client.GlobalSettingsJson = normalizedGlobal;
            changed = true;
        }

        if (!string.Equals(client.DeviceSettingsJson, normalizedDevices, StringComparison.Ordinal))
        {
            client.DeviceSettingsJson = normalizedDevices;
            changed = true;
        }

        if (!string.Equals(client.DisplayRegistrationsJson, normalizedDisplays, StringComparison.Ordinal))
        {
            client.DisplayRegistrationsJson = normalizedDisplays;
            changed = true;
        }
    }

    if (changed)
        await db.SaveChangesAsync();
}

static EmergencyCallUnified NormalizeIncidentForIngest(EmergencyCallUnified incident, ApiClient client)
{
    var nowUtc = DateTime.UtcNow;
    var details = incident.Details is null
        ? new Details(
            Agency: client.Name,
            Closed: false,
            Id: incident.GetCallIdentifier(),
            Source: client.ClientId,
            Status: "Open",
            CreatedAt: incident.GetCreatedAtUtc() ?? nowUtc,
            CreatedAtISO: incident.GetCreatedAtUtc() ?? nowUtc,
            UpdatedAt: incident.GetUpdatedAtUtc() ?? nowUtc,
            UpdatedAtISO: incident.GetUpdatedAtUtc() ?? nowUtc,
            DispatchedAt: null)
        : incident.Details with
        {
            Id = string.IsNullOrWhiteSpace(incident.Details.Id) ? incident.GetCallIdentifier() : incident.Details.Id,
            Source = string.IsNullOrWhiteSpace(incident.Details.Source) ? client.ClientId : incident.Details.Source,
            Agency = string.IsNullOrWhiteSpace(incident.Details.Agency) ? client.Name : incident.Details.Agency,
            Status = string.IsNullOrWhiteSpace(incident.Details.Status) ? (incident.Details.Closed == true ? "Closed" : "Open") : incident.Details.Status,
            CreatedAt = incident.Details.CreatedAt ?? incident.Details.CreatedAtISO ?? incident.GetCreatedAtUtc() ?? nowUtc,
            CreatedAtISO = incident.Details.CreatedAtISO ?? incident.Details.CreatedAt ?? incident.GetCreatedAtUtc() ?? nowUtc,
            UpdatedAt = incident.Details.UpdatedAt ?? incident.Details.UpdatedAtISO ?? incident.GetUpdatedAtUtc() ?? nowUtc,
            UpdatedAtISO = incident.Details.UpdatedAtISO ?? incident.Details.UpdatedAt ?? incident.GetUpdatedAtUtc() ?? nowUtc
        };

    var headers = incident.Headers is null && (!string.IsNullOrWhiteSpace(incident.Address) || !string.IsNullOrWhiteSpace(incident.GetCallType()) || !string.IsNullOrWhiteSpace(incident.GetPriority()))
        ? new Headers(
            Address: incident.GetAddress(),
            LocationName: incident.GetLocationName(),
            Coordinates: BuildCoordinates(incident),
            Latitude: incident.GetLatitude(),
            Longitude: incident.GetLongitude(),
            ApproximatedLocation: null,
            Priority: incident.GetPriority(),
            Type: incident.GetCallType())
        : incident.Headers;

    return incident with { Details = details, Headers = headers };
}

static string? BuildCoordinates(EmergencyCallUnified incident)
{
    if (!string.IsNullOrWhiteSpace(incident.Headers?.Coordinates))
        return incident.Headers.Coordinates;

    var latitude = incident.GetLatitude();
    var longitude = incident.GetLongitude();
    return latitude.HasValue && longitude.HasValue
        ? $"{latitude.Value},{longitude.Value}"
        : null;
}

static async Task EnsureIngestSchemaAsync(AppDbContext db)
{
    if (!db.Database.IsSqlServer())
        return;

    const string sql = @"
IF OBJECT_ID(N'[dbo].[IngestedIncidents]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[IngestedIncidents]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ApiClientId] INT NOT NULL,
        [AgencyId] INT NOT NULL,
        [IncidentId] NVARCHAR(128) NOT NULL,
        [CanonicalJson] NVARCHAR(MAX) NOT NULL,
        [Status] NVARCHAR(64) NULL,
        [IsClosed] BIT NOT NULL CONSTRAINT [DF_IngestedIncidents_IsClosed] DEFAULT(0),
        [Agency] NVARCHAR(256) NULL,
        [Address] NVARCHAR(512) NULL,
        [CallType] NVARCHAR(256) NULL,
        [UpdatedAtUtc] DATETIME2 NULL,
        [ReceivedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_IngestedIncidents_ReceivedAtUtc] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [FK_IngestedIncidents_ApiClients_ApiClientId] FOREIGN KEY ([ApiClientId]) REFERENCES [dbo].[ApiClients]([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_IngestedIncidents_ApiClientId_IncidentId] ON [dbo].[IngestedIncidents]([ApiClientId], [IncidentId]);
    CREATE INDEX [IX_IngestedIncidents_ApiClientId_UpdatedAtUtc] ON [dbo].[IngestedIncidents]([ApiClientId], [UpdatedAtUtc]);
END";

    await db.Database.ExecuteSqlRawAsync(sql);
}
static async Task<List<string>> DiscoverRetiredTablesAsync(AppDbContext db)
{
    var known = new[]
    {
        "CallRecords",
        "ClosedCallRecords",
        "OpenCallRecords",
        "CallDetails",
        "CallHistory",
        "PrintLogs",
        "EmailMessages",
        "ReceiveLogs"
    };

    if (!db.Database.IsSqlServer())
        return known.ToList();

    var conn = db.Database.GetDbConnection();
    var shouldClose = conn.State != System.Data.ConnectionState.Open;
    if (shouldClose) await conn.OpenAsync();
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'";
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            existing.Add(reader.GetString(0));

        return known.Where(existing.Contains).OrderBy(x => x).ToList();
    }
    catch
    {
        return known.ToList();
    }
    finally
    {
        if (shouldClose) await conn.CloseAsync();
    }
}

static string BuildRetiredTableDropScript(IReadOnlyList<string> retiredTables)
{
    if (retiredTables.Count == 0)
        return string.Empty;

    var sb = new StringBuilder();
    foreach (var table in retiredTables)
    {
        sb.AppendLine($"IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NOT NULL DROP TABLE [dbo].[{table}];");
    }

    return sb.ToString();
}

static JsonObject MergeObjects(JsonObject? existing, JsonObject incoming)
{
    var merged = existing?.DeepClone() as JsonObject ?? new JsonObject();
    foreach (var kvp in incoming)
        merged[kvp.Key] = kvp.Value?.DeepClone();
    return merged;
}

static void SetIfPresent(JsonObject target, string key, string? value)
{
    if (!string.IsNullOrWhiteSpace(value))
        target[key] = value;
}

public sealed class AuditMetadata
{
    public DateTime ChangedAtUtc { get; set; }
    public string? ChangedBy { get; set; }
    public string? Reason { get; set; }
    public string? Source { get; set; }
}

public sealed class GlobalSettingsDocument
{
    public JsonObject Settings { get; set; } = new();
    public AuditMetadata? Audit { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class DeviceProfile
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? StationCode { get; set; }
    public string? StationName { get; set; }
    public string? StartupUrl { get; set; }
    public string? DisplaySource { get; set; }
    public int? RefreshSeconds { get; set; }
    public string? PrinterRouting { get; set; }
    public string? DefaultPrinterName { get; set; }
    public string? CommandState { get; set; }
    public bool Enabled { get; set; } = true;
    public JsonObject Metadata { get; set; } = new();
}

public sealed class DeviceSettingsEntry
{
    public string DeviceId { get; set; } = "";
    public DeviceProfile? Profile { get; set; }
    public JsonObject Settings { get; set; } = new();
    public AuditMetadata? Audit { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class DisplayRegistrationEntry
{
    public string DeviceId { get; set; } = "";
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public bool Enabled { get; set; } = true;
    public JsonObject Settings { get; set; } = new();
    public DeviceProfile? Profile { get; set; }
    public AuditMetadata? Audit { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class DeviceResolvedConfiguration
{
    public string ClientId { get; set; } = "";
    public int ApiClientId { get; set; }
    public int AgencyId { get; set; }
    public string DeviceId { get; set; } = "";
    public DeviceProfile? Profile { get; set; }
    public JsonObject GlobalSettings { get; set; } = new();
    public JsonObject DeviceSettings { get; set; } = new();
    public AuditMetadata? GlobalSettingsAudit { get; set; }
    public AuditMetadata? DeviceAudit { get; set; }
    public AuditMetadata? DisplayAudit { get; set; }
    public DateTime? DeviceUpdatedAtUtc { get; set; }
    public DateTime? DisplayUpdatedAtUtc { get; set; }
}

public sealed class DeviceCommandEntry
{
    public string Id { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string Command { get; set; } = "";
    public string Status { get; set; } = DeviceCommandStatuses.Pending;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? LastHeartbeatAtUtc { get; set; }
    public string? ResultMessage { get; set; }
    public AuditMetadata? Audit { get; set; }
    public JsonObject Details { get; set; } = new();
}

public static class DeviceCommandStatuses
{
    public const string Pending = "pending";
    public const string Acknowledged = "acknowledged";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Stale = "stale";
}
static class JsonUtil
{
    public static readonly JsonSerializerOptions Options = EmergencyCallUnifiedJson.Options;
}

static class ScopeCatalog
{
    public const string ClientsRead = "clients.read";
    public const string ClientsWrite = "clients.write";
    public const string DisplayRead = "display.read";
    public const string DisplayWrite = "display.write";
    public const string DeviceRead = "device-settings.read";
    public const string DeviceWrite = "device-settings.write";
    public const string GlobalRead = "global-settings.read";
    public const string GlobalWrite = "global-settings.write";
    public const string KioskCommands = "kiosk.commands";
    public const string ServicePolling = "service.poll";
    public const string ReportAccess = "report.access";
    public const string Ingest = "call.ingest";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        ClientsRead,
        ClientsWrite,
        DisplayRead,
        DisplayWrite,
        DeviceRead,
        DeviceWrite,
        GlobalRead,
        GlobalWrite,
        KioskCommands,
        ServicePolling,
        ReportAccess,
        Ingest
    };

    public static readonly string[] DefaultClientScopes =
    {
        Ingest,
        DisplayRead,
        DisplayWrite,
        DeviceRead,
        DeviceWrite,
        GlobalRead,
        GlobalWrite,
        KioskCommands,
        ServicePolling,
        ReportAccess,
        ClientsRead,
        ClientsWrite
    };
}
