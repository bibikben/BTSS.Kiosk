using BTSS.IAR.Services;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;

namespace BTSS.IAR;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<AdminPreferencesStore>();
        builder.Services.AddSingleton<AdminSession>();
        builder.Services.AddSingleton<ServiceRuntimeFileStore>();
        builder.Services.AddSingleton<ServiceDatabaseReader>();
        builder.Services.AddSingleton<WindowsServiceBridge>();
        builder.Services.AddSingleton<EmbeddedPageResolver>();
        builder.Services.AddSingleton(sp => new HttpClient());
        builder.Services.AddSingleton<AdminApiClient>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
