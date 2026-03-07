using BTSS.IAR.Kiosk.Services;

#if WINDOWS
using BTSS.IAR.Kiosk.Platforms.Windows;
using BTSS.IAR.Kiosk.Platforms.Windows.Services;
#endif

namespace BTSS.IAR.Kiosk;

public partial class App : Application
{
    public Window? DisplayWindow { get; private set; }
    public IKioskBootstrapService BootstrapService { get; }

    public App(IKioskBootstrapService bootstrapService)
    {
        InitializeComponent();
        BootstrapService = bootstrapService;
        MainPage = new NavigationPage(new BootstrapPage());
    }

    public async Task<bool> TryBootstrapAndStartDisplayAsync(bool showBootstrapIfMissingConfig)
    {
#if WINDOWS
        var config = await BootstrapService.TryGetConfigurationAsync();
        if (config is null)
        {
            if (showBootstrapIfMissingConfig)
                ShowBootstrapWindow();
            return false;
        }

        var startupUrl = config.ResolveStartupUrl();
        if (string.IsNullOrWhiteSpace(startupUrl))
        {
            if (showBootstrapIfMissingConfig)
                ShowBootstrapWindow();
            return false;
        }

        var monitorIndex = config.ResolveMonitorIndex() ?? AppSettings.SelectedMonitorIndex;
        AppSettings.SavedUrl = startupUrl;
        AppSettings.SelectedMonitorIndex = monitorIndex;
        AppSettings.KioskDisplayName = config.Profile?.DisplayName;
        AppSettings.KioskLocation = config.Profile?.Location;
        AppSettings.KioskStationCode = config.Profile?.StationCode;
        AppSettings.KioskStationName = config.Profile?.StationName;

        await StartDisplayAsync(startupUrl, monitorIndex);
        return true;
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    public void ShowBootstrapWindow()
    {
#if WINDOWS
        if (Windows.Count > 0)
        {
            var tray = Windows[0].Handler?.MauiContext?.Services.GetService<ITrayService>();
            tray?.ShowAdmin();
        }
#endif

        if (MainPage is NavigationPage nav)
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await nav.PopToRootAsync(false);
                if (nav.Navigation.NavigationStack.LastOrDefault() is not BootstrapPage)
                    await nav.PushAsync(new BootstrapPage());
            });
        }
    }

    public DisplayPage StartDisplayWindow()
    {
        if (DisplayWindow != null)
            return (DisplayWindow.Page as DisplayPage)!;

        var page = new DisplayPage();
        DisplayWindow = new Window(page) { Title = "BTSS IAR Kiosk Display" };
        Application.Current!.OpenWindow(DisplayWindow);
        return page;
    }

#if WINDOWS
    public async Task StartDisplayAsync(string url, int monitorIndex)
    {
        var displayPage = StartDisplayWindow();
        await Task.Delay(150);

        var monitors = MonitorService.GetMonitors();
        var idx = Math.Clamp(monitorIndex, 0, Math.Max(0, monitors.Count - 1));
        if (monitors.Count > 0)
        {
            var monitor = monitors[idx];
            WindowKioskHelper.MakeKioskOnMonitor(DisplayWindow!, monitor);
        }

        displayPage.StartWatchdog();
        await displayPage.NavigateAndLoginIfNeededAsync(url, null, null, null);
    }
#endif

    public void StopDisplayWindow()
    {
        if (DisplayWindow == null) return;
        Application.Current!.CloseWindow(DisplayWindow);
        DisplayWindow = null;
    }
}
