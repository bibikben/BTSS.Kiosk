using BTSS.IAR.Kiosk.DispatchEmail.Reporting;
using BTSS.IAR.Kiosk.Services.DispatchEmail;
using BTSS.IAR.Kiosk.Services.IarApi;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Controls;

using WinRT.Interop;
using WebView = Microsoft.Maui.Controls.WebView;
using Microsoft.UI;
using CommunityToolkit.Maui.Core;



#if WINDOWS
using Microsoft.Maui;
using Microsoft.Maui.Controls.PlatformConfiguration.WindowsSpecific;
using Microsoft.Maui.Platform;
using BTSS.IAR.Kiosk.Platforms.Windows;
using BTSS.IAR.Kiosk.Services;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
using Application = Microsoft.Maui.Controls.Application;
using BTSS.IAR.Kiosk.Platforms.Windows.Services;
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
                .UseMauiCommunityToolkit()

                // After initializing the .NET MAUI Community Toolkit, optionally add additional fonts
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });


            // Dispatch Email / Clear Report pipeline
            builder.Services.AddSingleton<IDispatchReportRepository, DispatchReportRepository>();
	            builder.Services.AddSingleton<IFireStationClearReportParser, FireStationClearReportParser>();
	            builder.Services.AddSingleton<IPivotReportBuilder, PivotReportBuilder>();
	            builder.Services.AddSingleton<IPrintService, WindowsPrintService>();
            builder.Services.AddSingleton<IEmailCheckerService, GmailImapEmailCheckerService>();

            // IAR API polling pipeline (used when AppSettings.ProcessingMode == "IarApi")
            builder.Services.AddSingleton(sp =>
            {
                var baseUrl = (AppSettings.IarApiBaseUrl ?? "").Trim();
                if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = "http://localhost:5080";
                return new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
            });
            builder.Services.AddSingleton<IarTokenProvider>();
            builder.Services.AddSingleton<IIarApiClient, IarApiClient>();
            builder.Services.AddSingleton<IIarPivotReportBuilder, IarPivotReportBuilder>();
            builder.Services.AddSingleton<IIarPollingService, IarApiPollingService>();

            builder.Services.AddTransient<CallsPage>();
            builder.Services.AddTransient<AgencySetupPage>();
#if WINDOWS
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler(typeof(WebView), typeof(KioskWebViewHandler));
        });
        builder.Services.AddSingleton<IPrinterService, PrinterService>();
            builder.Services.AddSingleton<IAutoStartService, AutoStartService>();
        builder.Services.AddSingleton<ITrayService, TrayService>();
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
                    var hwnd = WindowNative.GetWindowHandle(winuiWindow);
                    // ✅ Set initial + minimum size for the Admin window
                    try
                    {
                        
                        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                        var appWindow = AppWindow.GetFromWindowId(windowId);

                        // Initial size
                        appWindow.Resize(new SizeInt32(900, 650));

                        // Minimum size (Windows App SDK 1.7+)
                        if (appWindow.Presenter is OverlappedPresenter p)
                        {
                            p.PreferredMinimumWidth = 800;
                            p.PreferredMinimumHeight = 600;
                        }
                    }
                    catch
                    {
                        // ignore sizing errors (older Windows App SDK builds can be picky)
                    }
                    // Use the app’s service provider (DI), not window.Handler
                    var services = MauiWinUIApplication.Current.Services;
                    var tray = services.GetService<ITrayService>();

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
