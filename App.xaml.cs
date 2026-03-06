using BTSS.IAR.Kiosk.Platforms.Windows.Services;
using BTSS.IAR.Kiosk.Services;
using BTSS.IAR.Kiosk.Services.DispatchEmail;
using BTSS.IAR.Kiosk.Services.IarApi;

#if WINDOWS
using BTSS.IAR.Kiosk.Platforms.Windows;
#endif

namespace BTSS.IAR.Kiosk;

public partial class App : Application
{
    public Window? DisplayWindow { get; private set; }
    private readonly IEmailCheckerService _emailChecker;
    private readonly IIarPollingService _iarPoller;

    public App(IEmailCheckerService emailChecker, IIarPollingService iarPoller, IPrinterService printerService)
    {
        InitializeComponent();
        _emailChecker = emailChecker;
        _iarPoller = iarPoller;
        MainPage = new NavigationPage(new BootstrapPage(this));
    }

    public async Task TryStartDisplayFromSavedAsync(bool showAdminIfMissingConfig)
    {
#if WINDOWS
        var creds = await CredentialStore.LoadAsync();
        if (creds == null || string.IsNullOrWhiteSpace(creds.Agency) || string.IsNullOrWhiteSpace(creds.Username))
        {
            if (showAdminIfMissingConfig)
                ShowAdminWindow();
            return;
        }

        if (AppSettings.SelectedMonitorIndex < 0)
        {
            if (showAdminIfMissingConfig)
                ShowAdminWindow();
            return;
        }

        _emailChecker.Stop();
        _iarPoller.Stop();
        if (string.Equals(AppSettings.ProcessingMode, "IarApi", StringComparison.OrdinalIgnoreCase))
            _iarPoller.Start();
        else
            _emailChecker.Start();

        await StartDisplayAsync(
            url: AppSettings.SavedUrl,
            agency: creds.Agency,
            username: creds.Username,
            password: creds.Password,
            monitorIndex: AppSettings.SelectedMonitorIndex);
#endif
    }

    public void ShowAdminWindow()
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
                if (nav.Navigation.NavigationStack.LastOrDefault() is not BootstrapPage)
                    await nav.PushAsync(new BootstrapPage(this));
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
    public async Task StartDisplayAsync(string url, string agency, string username, string password, int monitorIndex)
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
        await displayPage.NavigateAndLoginIfNeededAsync(url, agency, username, password);
    }
#endif

    public void StopDisplayWindow()
    {
        if (DisplayWindow == null) return;
        Application.Current!.CloseWindow(DisplayWindow);
        DisplayWindow = null;
    }
}
