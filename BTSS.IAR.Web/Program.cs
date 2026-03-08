using BTSS.IAR.Api.Auth;
using BTSS.IAR.Api.Data;
using BTSS.IAR.Web.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.EntityFrameworkCore;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Sql")
             ?? "Server=.\\SQLEXPRESS;Database=BTSS_IAR;Trusted_Connection=True;TrustServerCertificate=True";
    opt.UseSqlServer(cs);
});

builder.Services.AddScoped<HumanAuthService>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "btss.web.auth";
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddRazorComponents()
      .AddInteractiveServerComponents().AddHubOptions(options => options.MaximumReceiveMessageSize = 10 * 1024 * 1024);

builder.Services.AddControllers();
builder.Services.AddRadzenComponents();

builder.Services.AddRadzenCookieThemeService(options =>
{
    options.Name = "BTSS.IAR.WebTheme";
    options.Duration = TimeSpan.FromDays(365);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(nav.BaseUri) };
});
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var auth = scope.ServiceProvider.GetRequiredService<HumanAuthService>();
    await auth.SeedDefaultsAsync();
}

var forwardingOptions = new ForwardedHeadersOptions()
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
};
forwardingOptions.KnownIPNetworks.Clear();
forwardingOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardingOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapStaticAssets();
app.UseAntiforgery();

app.MapPost("/auth/login", async (HttpContext http, HumanAuthService auth, HumanLoginRequest request, CancellationToken ct) =>
{
    var user = await auth.ValidateCredentialsAsync(request.UserName, request.Password, ct);
    if (user is null)
        return Results.Unauthorized();

    var current = await auth.BuildCurrentUserAsync(user, request.AgencyId, ct);
    user.ActiveAgencyId = current.ActiveAgencyId;
    user.UpdatedAtUtc = DateTime.UtcNow;
    await http.RequestServices.GetRequiredService<AppDbContext>().SaveChangesAsync(ct);
    await auth.SignInAsync(http, current);
    return Results.Ok(current);
}).AllowAnonymous();

app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { success = true });
});

app.MapGet("/auth/me", async (HttpContext http, AppDbContext db, HumanAuthService auth, CancellationToken ct) =>
{
    if (!(http.User.Identity?.IsAuthenticated ?? false))
        return Results.Unauthorized();

    var userIdValue = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!int.TryParse(userIdValue, out var userId))
        return Results.Unauthorized();

    var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.IsEnabled, ct);
    if (user is null)
        return Results.Unauthorized();

    return Results.Ok(await auth.BuildCurrentUserAsync(user, user.ActiveAgencyId, ct));
});

app.MapPost("/auth/switch-agency", async (HttpContext http, AppDbContext db, HumanAuthService auth, SwitchAgencyRequest request, CancellationToken ct) =>
{
    if (!(http.User.Identity?.IsAuthenticated ?? false))
        return Results.Unauthorized();

    var userIdValue = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!int.TryParse(userIdValue, out var userId))
        return Results.Unauthorized();

    var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.IsEnabled, ct);
    if (user is null)
        return Results.Unauthorized();

    var hasMembership = await db.UserAgencies.AnyAsync(x => x.UserId == user.Id && x.AgencyId == request.AgencyId && x.IsEnabled, ct);
    if (!user.IsSuperUser && !hasMembership)
        return Results.Forbid();

    user.ActiveAgencyId = request.AgencyId;
    user.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync(ct);

    var current = await auth.BuildCurrentUserAsync(user, request.AgencyId, ct);
    await auth.SignInAsync(http, current);
    return Results.Ok(current);
});

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();
