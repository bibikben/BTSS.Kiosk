using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BTSS.IAR.Api.Data;
using BTSS.IAR.Api.Models;
using BTSS.IAR.Api.Models.Dtos;
using BTSS.IAR.Record.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BTSS IAR API", Version = "v1" });

    // XML comments (csproj enables generation)
    var xmlName = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlName);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    c.EnableAnnotations();
});

builder.Services.Configure<RoutingOptions>(builder.Configuration.GetSection("Routing"));
builder.Services.AddHttpClient("routing", c =>
{
    c.Timeout = TimeSpan.FromSeconds(15);
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

builder.Services.AddAuthorization();

builder.Services.AddSingleton(new TokenIssuer(jwtIssuer, signingKey));
builder.Services.AddSingleton<RouteMileageService>();
builder.Services.AddScoped<LookupUpsertService>();

var app = builder.Build();

// Ensure DB exists (dev convenience). If you prefer migrations, replace with db.Database.Migrate().
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// --- OAuth-ish token endpoint (client credentials)
// Request: { clientId, clientSecret, grantType: "client_credentials" }
// Response: { access_token, token_type, expires_in }
app.MapPost("/connect/token", async (AppDbContext db, TokenIssuer issuer, TokenRequest req) =>
{
    if (!string.Equals(req.GrantType, "client_credentials", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "unsupported_grant_type" });

    var client = await db.ApiClients.FirstOrDefaultAsync(c => c.ClientId == req.ClientId);
    if (client == null || !client.IsEnabled || client.ClientSecret != req.ClientSecret)
        return Results.Unauthorized();

    var token = issuer.IssueToken(new[]
    {
        new Claim("client_id", client.ClientId),
        new Claim("agency_id", client.AgencyId.ToString())
    }, expiresMinutes: 60);

    return Results.Ok(new { access_token = token, token_type = "Bearer", expires_in = 3600 });
});

// Seed endpoint (optional/dev): creates a client for an agency
app.MapPost("/dev/seedClient", async (AppDbContext db, SeedClientRequest req) =>
{
    var existing = await db.ApiClients.FirstOrDefaultAsync(x => x.ClientId == req.ClientId);
    if (existing != null) return Results.Conflict(new { message = "ClientId already exists" });

    var client = new ApiClient
    {
        ClientId = req.ClientId,
        ClientSecret = req.ClientSecret,
        AgencyId = req.AgencyId,
        IsEnabled = true
    };
    db.ApiClients.Add(client);

    var agency = await db.Agencies.FirstOrDefaultAsync(a => a.Id == req.AgencyId);
    if (agency == null)
    {
        db.Agencies.Add(new Agency { Id = req.AgencyId, Name = req.AgencyName ?? $"Agency {req.AgencyId}" });
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Seeded" });
});

// --- Protected API
var api = app.MapGroup("/api").RequireAuthorization();

// Typed ingestion endpoints (Swagger/Postman-friendly)
api.MapPost("/receiveCallDetails/iar", async (AppDbContext db, LookupUpsertService lookups, RouteMileageService mileage, ReceiveIarCallDetailsRequest req) =>
{
    if (req.AgencyIdentifier <= 0) return Results.BadRequest(new { message = "AgencyIdentifier required" });
    return await IngestIarAsync(db, lookups, mileage, req.AgencyIdentifier, req.Incident);
})
.WithName("ReceiveCallDetailsIar")
.WithOpenApi();

api.MapPost("/receiveCallDetails/emailText", async (AppDbContext db, LookupUpsertService lookups, RouteMileageService mileage, ReceiveEmailTextCallDetailsRequest req) =>
{
    if (req.AgencyIdentifier <= 0) return Results.BadRequest(new { message = "AgencyIdentifier required" });
    return await IngestEmailTextAsync(db, lookups, mileage, req.AgencyIdentifier, req.Body);
})
.WithName("ReceiveCallDetailsEmailText")
.WithOpenApi();
api.MapPost("/receiveCallDetails", async (AppDbContext db, RouteMileageService mileage, ReceiveCallDetailsRequest req) =>
{
    if (req.AgencyIdentifier <= 0) return Results.BadRequest(new { message = "AgencyIdentifier required" });

    string? callId;
    string? parsedAddress = null;
    string? parsedType = null;
    string? parsedPriority = null;
    double? lat = null;
    double? lng = null;
    bool? closed = null;
    DateTimeOffset? updatedAt = null;

    if (string.Equals(req.SystemIdentifier, "IAR", StringComparison.OrdinalIgnoreCase))
    {
        var record = System.Text.Json.JsonSerializer.Deserialize<IarCallRecord>(req.Payload, JsonUtil.Options);
        callId = record?.Details?.Id;

        parsedAddress = record?.Headers?.Address;
        parsedType = record?.Headers?.Type;
        parsedPriority = record?.Headers?.Priority;
        lat = record?.Headers?.Latitude;
        lng = record?.Headers?.Longitude;
        closed = record?.Details?.Closed;
        updatedAt = record?.Details?.UpdatedAt ?? record?.Details?.UpdatedAtISO;
    }
    else
    {
        // EmailText or other sources: store raw payload; caller can extend parsing later.
        callId = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(req.Payload)))
            .Replace("=", "")
            .Replace("+", "-")
            .Replace("/", "_")
            .Substring(0, 22);
    }

    if (string.IsNullOrWhiteSpace(callId))
        return Results.BadRequest(new { message = "Unable to determine CallIdentifier" });

    var existing = await db.Calls.FirstOrDefaultAsync(c => c.AgencyId == req.AgencyIdentifier && c.CallIdentifier == callId);
    if (existing == null)
    {
        existing = new CallRecordEntity
        {
            AgencyId = req.AgencyIdentifier,
            CallIdentifier = callId,
            SourceSystem = req.SystemIdentifier,
        };
        db.Calls.Add(existing);
    }

    existing.Payload = req.Payload;
    existing.ParsedAddress = parsedAddress;
    existing.ParsedType = parsedType;
    existing.ParsedPriority = parsedPriority;
    existing.Latitude = lat;
    existing.Longitude = lng;
    existing.IsClosed = closed ?? existing.IsClosed;
    existing.UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;

    // Mileage (best-effort): compute straight-line distance if station coords exist.
    var miles = await mileage.TryComputeMileageAsync(db, req.AgencyIdentifier, lat, lng);
    existing.EstimatedMilesFromStation = miles;

    await db.SaveChangesAsync();

    return Results.Ok(new ReceiveCallDetailsResponse
    {
        CallIdentifier = callId,
        EstimatedMilesFromStation = miles
    });
});

api.MapPost("/checkForClose", async (AppDbContext db, CheckForCloseRequest req) =>
{
    if (req.AgencyIdentifier <= 0) return Results.BadRequest(new { message = "AgencyIdentifier required" });

    var state = await db.PollStates.FirstOrDefaultAsync(s => s.AgencyId == req.AgencyIdentifier);
    var lastCheck = state?.LastCloseCheckUtc ?? DateTimeOffset.MinValue;

    var closings = await db.Calls
        .Where(c => c.AgencyId == req.AgencyIdentifier && c.IsClosed && c.UpdatedAt > lastCheck)
        .OrderBy(c => c.UpdatedAt)
        .Select(c => c.CallIdentifier)
        .ToListAsync();

    if (state == null)
    {
        state = new PollState { AgencyId = req.AgencyIdentifier };
        db.PollStates.Add(state);
    }
    state.LastCloseCheckUtc = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(new { callIdentifiers = closings });
});

api.MapGet("/getCallRecord", async (AppDbContext db, int agencyId, string callIdentifier) =>
{
    var rec = await db.Calls.FirstOrDefaultAsync(c => c.AgencyId == agencyId && c.CallIdentifier == callIdentifier);
    if (rec == null) return Results.NotFound();

    // If IAR, return the formatted JSON (as stored).
    if (string.Equals(rec.SourceSystem, "IAR", StringComparison.OrdinalIgnoreCase))
        return Results.Text(rec.Payload, "application/json");

    // Otherwise, wrap raw payload.
    return Results.Ok(new { callIdentifier = rec.CallIdentifier, payload = rec.Payload, source = rec.SourceSystem });
});

api.MapGet("/getListOfCalls", async (AppDbContext db, int agencyId, DateTimeOffset? startDate, DateTimeOffset? endDate, string? callType) =>
{
    var q = db.Calls.AsNoTracking().Where(c => c.AgencyId == agencyId);
    if (startDate != null) q = q.Where(c => c.UpdatedAt >= startDate);
    if (endDate != null) q = q.Where(c => c.UpdatedAt <= endDate);
    if (!string.IsNullOrWhiteSpace(callType)) q = q.Where(c => c.ParsedType != null && c.ParsedType.Contains(callType));

    var list = await q
        .OrderByDescending(c => c.UpdatedAt)
        .Take(250)
        .Select(c => new
        {
            callIdentifier = c.CallIdentifier,
            type = c.ParsedType,
            address = c.ParsedAddress,
            closed = c.IsClosed,
            updatedAt = c.UpdatedAt
        })
        .ToListAsync();

    return Results.Ok(list);
});

// --- Agency setup endpoints (station coordinates for mileage)
api.MapGet("/agencies", async (AppDbContext db) =>
{
    var list = await db.Agencies
        .AsNoTracking()
        .OrderBy(a => a.Id)
        .Select(a => new
        {
            id = a.Id,
            name = a.Name,
            stationLatitude = a.StationLatitude,
            stationLongitude = a.StationLongitude
        })
        .ToListAsync();

    return Results.Ok(list);
});

api.MapGet("/agency/{id:int}", async (AppDbContext db, int id) =>
{
    var a = await db.Agencies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (a == null) return Results.NotFound();
    return Results.Ok(new { id = a.Id, name = a.Name, stationLatitude = a.StationLatitude, stationLongitude = a.StationLongitude });
});

api.MapPost("/agency", async (AppDbContext db, AgencyUpsertRequest req) =>
{
    if (req.Id <= 0) return Results.BadRequest(new { message = "Id required" });

    var existing = await db.Agencies.FirstOrDefaultAsync(a => a.Id == req.Id);
    if (existing != null) return Results.Conflict(new { message = "Agency already exists" });

    var a = new Agency
    {
        Id = req.Id,
        Name = req.Name ?? $"Agency {req.Id}",
        StationLatitude = req.StationLatitude,
        StationLongitude = req.StationLongitude
    };
    db.Agencies.Add(a);
    await db.SaveChangesAsync();
    return Results.Ok(new { id = a.Id });
});

api.MapPut("/agency/{id:int}", async (AppDbContext db, int id, AgencyUpsertRequest req) =>
{
    var a = await db.Agencies.FirstOrDefaultAsync(x => x.Id == id);
    if (a == null) return Results.NotFound();

    if (!string.IsNullOrWhiteSpace(req.Name)) a.Name = req.Name;
    a.StationLatitude = req.StationLatitude;
    a.StationLongitude = req.StationLongitude;

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Updated" });
});

app.Run();

// ------------------
// Ingestion helpers
// ------------------

static async Task<IResult> IngestIarAsync(AppDbContext db, LookupUpsertService lookups, RouteMileageService mileage, int agencyIdentifier, IarIncidentDto incident)
{
    var callId = incident.Details?.Id;
    if (string.IsNullOrWhiteSpace(callId))
        return Results.BadRequest(new { message = "Unable to determine CallIdentifier" });

    var sourceSystem = "IAR";
    var payload = System.Text.Json.JsonSerializer.Serialize(incident, JsonUtil.Options);

    var parsedAddress = incident.Headers?.Address;
    var parsedType = incident.Headers?.Type;
    var parsedPriority = incident.Headers?.Priority;
    var lat = incident.Headers?.Latitude;
    var lng = incident.Headers?.Longitude;
    var closed = incident.Details?.Closed;
    var updatedAt = incident.Details?.UpdatedAt ?? incident.Details?.UpdatedAtISO ?? DateTimeOffset.UtcNow;

    // Ensure lookup rows exist
    var sourceSystemId = await lookups.EnsureSourceSystemAsync(sourceSystem);
    var priorityId = await lookups.EnsurePriorityAsync(parsedPriority);
    var callTypeId = await lookups.EnsureCallTypeAsync(parsedType);
    var cadAgencyId = await lookups.EnsureCadAgencyAsync(incident.Details?.Agency);
    var statusId = await lookups.EnsureCallStatusAsync(incident.Details?.Status);

    var existing = await db.Calls
        .Include(c => c.Units)
        .FirstOrDefaultAsync(c => c.AgencyId == agencyIdentifier && c.CallIdentifier == callId);

    if (existing == null)
    {
        existing = new CallRecordEntity
        {
            AgencyId = agencyIdentifier,
            CallIdentifier = callId,
            SourceSystem = sourceSystem,
            SourceSystemId = sourceSystemId,
        };
        db.Calls.Add(existing);
    }

    existing.Payload = payload;
    existing.ParsedAddress = parsedAddress;
    existing.ParsedType = parsedType;
    existing.ParsedPriority = parsedPriority;
    existing.Latitude = lat;
    existing.Longitude = lng;
    existing.IsClosed = closed ?? existing.IsClosed;
    existing.UpdatedAt = updatedAt;

    existing.PriorityId = priorityId;
    existing.CallTypeId = callTypeId;
    existing.CadAgencyId = cadAgencyId;
    existing.StatusId = statusId;

    // Units: insert status events (best-effort). If this becomes too noisy, add a dedupe key.
    if (incident.Units != null)
    {
        foreach (var u in incident.Units)
        {
            var unitId = await lookups.EnsureUnitAsync(u.Id);
            var unitStatusId = await lookups.EnsureUnitStatusAsync(u.Status);
            var unitStatusOriginalId = await lookups.EnsureUnitStatusAsync(u.StatusOriginal);

            if (unitId == null || unitStatusId == null) continue;

            existing.Units.Add(new CallUnitEntity
            {
                UnitId = unitId.Value,
                StatusId = unitStatusId.Value,
                StatusOriginalId = unitStatusOriginalId,
                CreatedAt = u.CreatedAt ?? u.CreatedAtISO
            });
        }
    }

    // Mileage (best-effort)
    var miles = await mileage.TryComputeMileageAsync(db, agencyIdentifier, lat, lng);
    existing.EstimatedMilesFromStation = miles;

    await db.SaveChangesAsync();

    return Results.Ok(new ReceiveCallDetailsResponse
    {
        CallIdentifier = callId,
        EstimatedMilesFromStation = miles
    });
}

static async Task<IResult> IngestEmailTextAsync(AppDbContext db, LookupUpsertService lookups, RouteMileageService mileage, int agencyIdentifier, string body)
{
    var sourceSystem = "EmailText";
    var callId = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(body ?? string.Empty)))
        .Replace("=", "")
        .Replace("+", "-")
        .Replace("/", "_")
        .Substring(0, 22);

    var sourceSystemId = await lookups.EnsureSourceSystemAsync(sourceSystem);

    var existing = await db.Calls.FirstOrDefaultAsync(c => c.AgencyId == agencyIdentifier && c.CallIdentifier == callId);
    if (existing == null)
    {
        existing = new CallRecordEntity
        {
            AgencyId = agencyIdentifier,
            CallIdentifier = callId,
            SourceSystem = sourceSystem,
            SourceSystemId = sourceSystemId,
        };
        db.Calls.Add(existing);
    }

    existing.Payload = body ?? string.Empty;
    existing.UpdatedAt = DateTimeOffset.UtcNow;

    await db.SaveChangesAsync();

    return Results.Ok(new ReceiveCallDetailsResponse
    {
        CallIdentifier = callId,
        EstimatedMilesFromStation = null
    });
}
// ---- helpers
static class JsonUtil
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
}
