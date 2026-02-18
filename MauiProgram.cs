using Microsoft.Extensions.Logging;
using BTSS.IAR.Kiosk.Services.DispatchEmail;
#if WINDOWS
using BTSS.IAR.Kiosk.Platforms.Windows;
using BTSS.IAR.Kiosk.Services;
using Microsoft.Maui.LifecycleEvents;
#endif

namespace BTSS.IAR.Kiosk
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
#if WINDOWS
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BTSS.IAR.Kiosk",
            "WebView2Profile");

        Directory.CreateDirectory(folder);

        // Pin WebView2 cookies/session/profile to this folder
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", folder, EnvironmentVariableTarget.Process);
#endif
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

	            // Dispatch Email / Clear Report pipeline
            builder.Services.AddSingleton<IDispatchReportRepository, DispatchReportRepository>();
	            builder.Services.AddSingleton<IFireStationClearReportParser, FireStationClearReportParser>();
	            builder.Services.AddSingleton<IPivotReportBuilder, PivotReportBuilder>();
	            builder.Services.AddSingleton<IPrinterService, WindowsPrinterService>();
            builder.Services.AddSingleton<IEmailCheckerService, GmailImapEmailCheckerService>();
#if WINDOWS
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler(typeof(WebView), typeof(KioskWebViewHandler));
        });
        builder.Services.AddSingleton<IAutoStartService, AutoStartService>();
        builder.Services.AddSingleton<ITrayIconService, TrayIconService>();
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddWindows(w =>
            {
                w.OnLaunched((app, args) =>
                {
                    // detect autostart invocation
                    var cmd = Environment.GetCommandLineArgs();
                    RuntimeFlags.IsAutoStartInvocation = cmd.Any(a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));
                });

                w.OnWindowCreated(window =>
                {
                    // Initialize system tray and close-to-tray behavior
                    var services = window..Handler?.MauiContext?.Services;
                    var tray = services?.GetService<ITrayIconService>();
                    tray?.Initialize(window);

                    // Start minimized (either explicitly set, or autostart invocation)
                    if (AppSettings.StartMinimized || RuntimeFlags.IsAutoStartInvocation)
                    {
                        MainThread.BeginInvokeOnMainThread(() => tray?.HideAdmin());
                    }

                    // If configured, start display automatically once window exists.
                    if (AppSettings.StartDisplayOnStartup && AppSettings.SelectedMonitorIndex >= 0)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            if (Application.Current is App kioskApp)
                                await kioskApp.TryStartDisplayFromSavedAsync(showAdminIfMissingConfig: true);
                        });
                    }
                });
            });
        });
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
