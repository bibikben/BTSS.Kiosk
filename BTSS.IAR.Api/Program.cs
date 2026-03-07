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

builder.Services.AddRazorPages();
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

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapRazorPages();

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

api.MapPost("/receiveCallDetails/iar", async (HttpContext http) =>
{
    var (ok, body, err) = await ReadBodySafeAsync(http.Request, maxBytes: 1024 * 1024);
    if (!ok) return Results.BadRequest(new { message = err });

    EmergencyCallUnified? incident;
    try
    {
        var req = JsonSerializer.Deserialize<ReceiveIarCallDetailsRequest>(body, JsonUtil.Options);
        incident = req?.Incident;
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { message = "Invalid JSON" });
    }

    if (incident is null)
        return Results.BadRequest(new { message = "Missing Incident" });

    return Results.Ok(new ReceiveCallDetailsResponse
    {
        CallIdentifier = incident.GetCallIdentifier() ?? "",
        Persisted = false,
        Message = "Epic 3 removed API call persistence. Payload validated but not stored in the API database."
    });
})
.RequireAuthorization("scope:ingest")
.RequireRateLimiting("iar-ingest");

api.MapPost("/receiveCallDetails/emailText", (ReceiveEmailTextCallDetailsRequest req) =>
{
    var callId = StableHash(req.Body ?? string.Empty);
    return Results.Ok(new ReceiveCallDetailsResponse
    {
        CallIdentifier = callId,
        Persisted = false,
        Message = "Epic 3 removed API call persistence. Email payload accepted but not stored in the API database."
    });
})
.RequireAuthorization("scope:ingest")
.RequireRateLimiting("iar-ingest");

api.MapPost("/receiveCallDetails", (ReceiveCallDetailsRequest req) =>
{
    var callId = string.Equals(req.SystemIdentifier, "IAR", StringComparison.OrdinalIgnoreCase)
        ? EmergencyCallUnifiedJson.Deserialize(req.Payload)?.GetCallIdentifier() ?? StableHash(req.Payload ?? string.Empty)
        : StableHash(req.Payload ?? string.Empty);

    return Results.Ok(new ReceiveCallDetailsResponse
    {
        CallIdentifier = callId,
        Persisted = false,
        Message = "Epic 3 removed API call persistence. Use the Windows service/local database for incident history and close detection."
    });
})
.RequireAuthorization("scope:ingest")
.RequireRateLimiting("iar-ingest");

api.MapPost("/checkForClose", () => Results.Ok(new
{
    message = "checkForClose is no longer available in the API because call persistence was removed in Epic 3. Perform close detection in the Windows service local database."
}))
.RequireAuthorization("scope:service");

api.MapGet("/getCallRecord", () => Results.Ok(new
{
    message = "getCallRecord is no longer available in the API because call persistence was removed in Epic 3."
}))
.RequireAuthorization("scope:service");

api.MapGet("/getListOfCalls", () => Results.Ok(new
{
    message = "getListOfCalls is no longer available in the API because call persistence was removed in Epic 3."
}))
.RequireAuthorization("scope:service");

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
    UPDATE ApiClients
    SET ClientSecretRotatedAtUtc = ISNULL(ClientSecretRotatedAtUtc, UpdatedAtUtc)
    WHERE ClientSecret IS NOT NULL AND ClientSecret <> '' AND (ClientSecretHash = '' OR ClientSecretSalt = '');
END";

    await db.Database.ExecuteSqlRawAsync(sql);

    var legacyClients = await db.ApiClients.Where(x => (x.ClientSecretHash == "" || x.ClientSecretSalt == "")).ToListAsync();
    foreach (var client in legacyClients)
    {
        var legacySecret = await TryReadLegacyClientSecretAsync(db, client.Id);
        if (!string.IsNullOrWhiteSpace(legacySecret))
        {
            client.SetClientSecret(legacySecret!);
            client.UpdatedAtUtc = DateTime.UtcNow;
        }

        if (string.IsNullOrWhiteSpace(client.AllowedScopesJson) || client.AllowedScopesJson == "{}")
            client.AllowedScopesJson = JsonSerializer.Serialize(ScopeCatalog.DefaultClientScopes);
    }

    if (legacyClients.Count > 0)
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
