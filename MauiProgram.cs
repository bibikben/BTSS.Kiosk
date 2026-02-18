using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Controls;
using BTSS.IAR.Kiosk.Services.DispatchEmail;
using WinRT.Interop;
using WebView = Microsoft.Maui.Controls.WebView;

#if WINDOWS
using Microsoft.Maui;
using Microsoft.Maui.Controls.PlatformConfiguration.WindowsSpecific;
using Microsoft.Maui.Platform;
using BTSS.IAR.Kiosk.Platforms.Windows;
using BTSS.IAR.Kiosk.Services;
using Microsoft.Maui.LifecycleEvents;
using Application = Microsoft.Maui.Controls.Application;
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

                w.OnWindowCreated(winuiWindow =>
                {
                    // Use the app’s service provider (DI), not window.Handler
                    var services = MauiWinUIApplication.Current.Services;
                    var tray = services.GetService<ITrayIconService>();

                    // IMPORTANT: pass the WinUI window (or hwnd), not a MAUI Window
                    var hwnd = WindowNative.GetWindowHandle(winuiWindow);
                    tray?.Initialize(hwnd);

                    if (AppSettings.StartMinimized || RuntimeFlags.IsAutoStartInvocation)
                    {
                        MainThread.BeginInvokeOnMainThread(() => tray?.HideAdmin());
                    }

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
