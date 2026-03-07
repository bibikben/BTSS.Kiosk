using BTSS.Service.Data;
using BTSS.Service.Options;
using BTSS.Service.Services;
using BTSS.Service.Workers;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "BTSS Service";
});

builder.Services.AddOptions<ServiceRuntimeOptions>()
    .Bind(builder.Configuration.GetSection(ServiceRuntimeOptions.SectionName))
    .Validate(options => options.Validate(out _), "Service settings are invalid.")
    .ValidateOnStart();

builder.Services.AddDbContext<ServiceDbContext>((sp, options) =>
{
    var runtime = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServiceRuntimeOptions>>().Value;
    var dbPath = runtime.ResolveDatabasePath();
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    options.UseSqlite($"Data Source={dbPath}");
});

builder.Services.AddHttpClient<OAuthTokenClient>();
builder.Services.AddHttpClient<IncidentPollingClient>((sp, client) =>
{
    var runtime = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServiceRuntimeOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(runtime.ApiBaseUrl))
    {
        client.BaseAddress = new Uri(runtime.ApiBaseUrl, UriKind.Absolute);
    }
    client.Timeout = TimeSpan.FromSeconds(Math.Max(15, runtime.HttpTimeoutSeconds));
});

builder.Services.AddScoped<IncidentStore>();
builder.Services.AddSingleton<PrintTemplateRenderer>();
builder.Services.AddScoped<PrintJobWriter>();
builder.Services.AddScoped<IPrintDispatcher, PrintDispatcher>();
builder.Services.AddHostedService<CallPollingWorker>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();
    await db.Database.EnsureCreatedAsync();
}
await app.RunAsync();
