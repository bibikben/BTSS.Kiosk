using BTSS.IAR.Kiosk.Services;
using BTSS.IAR.Kiosk.Services.DispatchEmail;
using Application = Microsoft.Maui.Controls.Application;
#if WINDOWS
using BTSS.IAR.Kiosk.Platforms.Windows;
#endif
namespace BTSS.IAR.Kiosk;

public partial class App : Application
{
    public Window? DisplayWindow { get; private set; }

    [Obsolete("Obsolete")]
    public App(IEmailCheckerService emailChecker)
    {
        InitializeComponent();
        MainPage = new NavigationPage(new AdminPage(this, emailChecker));
    }
    public async Task TryStartDisplayFromSavedAsync(bool showAdminIfMissingConfig)
    {
#if WINDOWS
        // Need credentials + saved URL + saved monitor
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
            // Attempt to show/activate main window
            var tray = Windows[0].Handler?.MauiContext?.Services.GetService<ITrayService>();
            tray?.ShowAdmin();
        }
#endif
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

        // Give MAUI a moment to create native window
        await Task.Delay(150);

        var monitors = MonitorService.GetMonitors();
        var idx = Math.Clamp(monitorIndex, 0, Math.Max(0, monitors.Count - 1));
        if (monitors.Count > 0)
        {
            var monitor = monitors[idx];
            WindowKioskHelper.MakeKioskOnMonitor(DisplayWindow!, monitor);
        }

        displayPage.StartWatchdog();

        await displayPage.NavigateAndLoginIfNeededAsync(
            url,
            agency,
            username,
            password);
    }
#endif
    public void StopDisplayWindow()
    {
        if (DisplayWindow == null) return;
        Application.Current!.CloseWindow(DisplayWindow);
        DisplayWindow = null;
    }
}