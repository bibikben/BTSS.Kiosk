using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;
using BTSS.IAR.Kiosk.Services.IarApi;

using WinRT.Interop;
using WebView = Microsoft.Maui.Controls.WebView;
using BTSS.IAR.Kiosk.Services;
using Microsoft.UI;



#if WINDOWS
using BTSS.IAR.Kiosk.Platforms.Windows;
using BTSS.IAR.Kiosk.Platforms.Windows.Services;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.UI.Windowing;
using Windows.Graphics;
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
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", folder, EnvironmentVariableTarget.Process);
#endif
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton(_ => new HttpClient());
            builder.Services.AddSingleton<IarTokenProvider>();
            builder.Services.AddSingleton<IKioskBootstrapService, KioskBootstrapService>();

#if WINDOWS
            builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler(typeof(WebView), typeof(KioskWebViewHandler));
            });
            builder.Services.AddSingleton<ITrayService, TrayService>();
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddWindows(w =>
                {
                    w.OnLaunched((app, args) =>
                    {
                        var cmd = Environment.GetCommandLineArgs();
                        RuntimeFlags.IsAutoStartInvocation = cmd.Any(a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));
                    });

                    w.OnWindowCreated(winuiWindow =>
                    {
                        var hwnd = WindowNative.GetWindowHandle(winuiWindow);
                        try
                        {
                            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                            var appWindow = AppWindow.GetFromWindowId(windowId);
                            appWindow.Resize(new SizeInt32(900, 650));

                            if (appWindow.Presenter is OverlappedPresenter p)
                            {
                                p.PreferredMinimumWidth = 800;
                                p.PreferredMinimumHeight = 600;
                            }
                        }
                        catch
                        {
                        }

                        var services = MauiWinUIApplication.Current.Services;
                        var tray = services.GetService<ITrayService>();
                        tray?.Initialize(hwnd);

                        if (Services.AppSettings.StartMinimized || RuntimeFlags.IsAutoStartInvocation)
                        {
                            MainThread.BeginInvokeOnMainThread(() => tray?.HideAdmin());
                        }

                        if (Services.AppSettings.StartDisplayOnStartup)
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                if (Application.Current is App kioskApp)
                                    await kioskApp.TryBootstrapAndStartDisplayAsync(showBootstrapIfMissingConfig: true);
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
