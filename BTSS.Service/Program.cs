using BTSS.Service.Data;
using BTSS.Service.Options;
using BTSS.Service.Services;
using BTSS.Service.Workers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "BTSS Service";
});

builder.Services.AddOptions<ServiceRuntimeOptions>()
    .Bind(builder.Configuration.GetSection(ServiceRuntimeOptions.SectionName))
    .Validate(options => options.Validate(out _), "Service settings are invalid.")
    .ValidateOnStart();

builder.Services.PostConfigure<ServiceRuntimeOptions>(options =>
{
    LegacyServiceOptionsCompatibility.Apply(builder.Configuration, options);

    if (string.IsNullOrWhiteSpace(options.ApiBaseUrl) ||
        !Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _))
    {
        throw new InvalidOperationException("Service:ApiBaseUrl must be a valid absolute URI.");
    }
});

builder.Services.AddDbContext<ServiceDbContext>((sp, options) =>
{
    var runtime = sp.GetRequiredService<IOptions<ServiceRuntimeOptions>>().Value;
    var dbPath = runtime.ResolveDatabasePath();
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    options.UseSqlite($"Data Source={dbPath}");
});

builder.Services.AddHttpClient<OAuthTokenClient>((sp, client) =>
{
    var runtime = sp.GetRequiredService<IOptions<ServiceRuntimeOptions>>().Value;

    if (!string.IsNullOrWhiteSpace(runtime.ApiBaseUrl))
    {
        client.BaseAddress = new Uri(runtime.ApiBaseUrl, UriKind.Absolute);
    }

    client.Timeout = TimeSpan.FromSeconds(Math.Max(15, runtime.HttpTimeoutSeconds));
});

builder.Services.AddHttpClient<IncidentPollingClient>((sp, client) =>
{
    var runtime = sp.GetRequiredService<IOptions<ServiceRuntimeOptions>>().Value;

    if (!string.IsNullOrWhiteSpace(runtime.ApiBaseUrl))
    {
        client.BaseAddress = new Uri(runtime.ApiBaseUrl, UriKind.Absolute);
    }

    client.Timeout = TimeSpan.FromSeconds(Math.Max(15, runtime.HttpTimeoutSeconds));
});

builder.Services.AddHttpClient<DeviceSyncClient>((sp, client) =>
{
    var runtime = sp.GetRequiredService<IOptions<ServiceRuntimeOptions>>().Value;

    if (!string.IsNullOrWhiteSpace(runtime.ApiBaseUrl))
    {
        client.BaseAddress = new Uri(runtime.ApiBaseUrl, UriKind.Absolute);
    }

    client.Timeout = TimeSpan.FromSeconds(Math.Max(15, runtime.HttpTimeoutSeconds));
});

builder.Services.AddHttpClient<SftpIncidentIngestClient>((sp, client) =>
{
    var runtime = sp.GetRequiredService<IOptions<ServiceRuntimeOptions>>().Value;

    if (!string.IsNullOrWhiteSpace(runtime.ApiBaseUrl))
    {
        client.BaseAddress = new Uri(runtime.ApiBaseUrl, UriKind.Absolute);
    }

    client.Timeout = TimeSpan.FromSeconds(Math.Max(15, runtime.HttpTimeoutSeconds));
});
builder.Services.AddScoped<IncidentStore>();
builder.Services.AddSingleton<PrintTemplateRenderer>();
builder.Services.AddScoped<PrintJobWriter>();
builder.Services.AddScoped<LocalSyncStore>();
builder.Services.AddScoped<IPrintDispatcher, PrintDispatcher>();
builder.Services.AddSingleton<SftpConnectionFactory>();
builder.Services.AddScoped<SftpIncidentFileScanner>();
builder.Services.AddScoped<SftpImportLedger>();
builder.Services.AddHostedService<CallPollingWorker>();
builder.Services.AddHostedService<SftpIncidentImportWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();
    await ServiceDbSchemaInitializer.InitializeAsync(db);
}

await app.RunAsync();