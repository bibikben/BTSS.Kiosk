#if WINDOWS
using BTSS.IAR.Kiosk.Platforms.Windows;
using Microsoft.Maui.Platform;
using WinRT.Interop;
#endif
using BTSS.IAR.Kiosk.Services;
using BTSS.IAR.Kiosk.Services.DispatchEmail;
using Application = Microsoft.Maui.Controls.Application;
#if WINDOWS
using Microsoft.Extensions.DependencyInjection;
#endif

namespace BTSS.IAR.Kiosk;

public partial class AdminPage : ContentPage
{
#if WINDOWS
    private List<MonitorInfo> _monitors = new();
    private GlobalHotKey? _hotKey;
#endif

    private readonly App _app;
    private readonly IEmailCheckerService _emailChecker;
#if WINDOWS
    private bool _eventsWired;
#endif

#if WINDOWS
    private IAutoStartService? _autoStart;
#endif

    public AdminPage(App app, IEmailCheckerService emailChecker)
    {
        InitializeComponent();
        _app = app;
        _emailChecker = emailChecker;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

#if WINDOWS
        _autoStart ??= this.Handler?.MauiContext?.Services.GetService<IAutoStartService>()
                       ?? Application.Current?.Windows.FirstOrDefault()?.Handler?.MauiContext?.Services.GetService<IAutoStartService>()
                       ?? new AutoStartService();

        // Settings switches
        AutoStartSwitch.IsToggled = _autoStart.IsEnabled();
        StartDisplayOnStartupSwitch.IsToggled = AppSettings.StartDisplayOnStartup;
        StartMinimizedSwitch.IsToggled = AppSettings.StartMinimized;

        if (!_eventsWired)
        {
            _eventsWired = true;
            AutoStartSwitch.Toggled += (_, e) =>
            {
                _autoStart?.SetEnabled(e.Value);
                AppSettings.AutoStartAtLogin = e.Value;
            };

            StartDisplayOnStartupSwitch.Toggled += (_, e) => AppSettings.StartDisplayOnStartup = e.Value;
            StartMinimizedSwitch.Toggled += (_, e) => AppSettings.StartMinimized = e.Value;
        }
        // Load saved creds


        var saved = await CredentialStore.LoadAsync();
        if (saved != null)
        {
            AgencyEntry.Text = saved.Agency;
            UserEntry.Text = saved.Username;
            PassEntry.Text = saved.Password;
        }
        // Load saved Gmail creds
        UrlEntry.Text = AppSettings.SavedUrl;
   // Monitor picker
        _monitors = MonitorService.GetMonitors();
        MonitorPicker.ItemsSource = _monitors
            .Select((m, i) => $"{i}: {m.DeviceName} {(m.IsPrimary ? "(Primary)" : "")} [{m.Width}x{m.Height}]")
            .ToList();

        var savedMonitor = AppSettings.SelectedMonitorIndex;
        if (savedMonitor >= 0 && savedMonitor < _monitors.Count)
            MonitorPicker.SelectedIndex = savedMonitor;
        else
            MonitorPicker.SelectedIndex = _monitors.Count > 1 ? 1 : 0;

        // Always keep monitor selection remembered
        MonitorPicker.SelectedIndexChanged -= OnMonitorChanged;
        MonitorPicker.SelectedIndexChanged += OnMonitorChanged;
       
	   
	   
	   
	   
	   
	   
	    var emailSaved = await DispatchEmailCredentialStore.LoadAsync();
        if (emailSaved != null)
        {
            GmailAddressEntry.Text = emailSaved.EmailAddress;
            GmailAppPasswordEntry.Text = emailSaved.AppPassword;
        }
        RegisterAdminHotKey();
#endif
    }
#if WINDOWS
    private void OnMonitorChanged(object? sender, EventArgs e)
    {
        if (MonitorPicker.SelectedIndex >= 0)
            AppSettings.SelectedMonitorIndex = MonitorPicker.SelectedIndex;
    }
#endif

#if WINDOWS
    private void RegisterAdminHotKey()
    {
        if (_hotKey != null) return;

        var mauiWindow = this.Window;
        if (mauiWindow?.Handler?.PlatformView is not MauiWinUIWindow winuiWindow)
            return;

        var hwnd = WindowNative.GetWindowHandle(winuiWindow);

        // Ctrl+Shift+A
        const uint VK_A = 0x41;
        _hotKey = new GlobalHotKey(hwnd, id: 0xBEEF,
            modifiers: GlobalHotKey.MOD_CONTROL | GlobalHotKey.MOD_SHIFT,
            vk: VK_A);

        _hotKey.Pressed += () =>
        {
            MainThread.BeginInvokeOnMainThread(() => winuiWindow.Activate());
        };
    }
#endif

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if WINDOWS
        _hotKey?.Dispose();
        _hotKey = null;
#endif
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
#if WINDOWS
        AppSettings.SavedUrl = UrlEntry.Text ?? AppSettings.SavedUrl;
        AppSettings.SelectedMonitorIndex = MonitorPicker.SelectedIndex;
        // Save creds securely
        await CredentialStore.SaveAsync(new StoredCreds(
            AgencyEntry.Text ?? "",
            UserEntry.Text ?? "",
            PassEntry.Text ?? ""
        ));

            await DispatchEmailCredentialStore.SaveAsync(new DispatchEmailCreds(
            GmailAddressEntry.Text ?? "",
            GmailAppPasswordEntry.Text ?? ""
            ));

        await _app.StartDisplayAsync(
            url: UrlEntry.Text ?? "",
            agency: AgencyEntry.Text ?? "",
            username: UserEntry.Text ?? "",
            password: PassEntry.Text ?? "",
            monitorIndex: MonitorPicker.SelectedIndex);;
#endif
    }

    private void OnStopClicked(object sender, EventArgs e)
    {
        if (_app.DisplayWindow?.Page is DisplayPage dp)
            dp.StopWatchdog();

        _emailChecker.Stop();
        _app.StopDisplayWindow();
    }

    private void OnClearCredsClicked(object sender, EventArgs e)
    {
#if WINDOWS
        CredentialStore.Clear();
		DispatchEmailCredentialStore.Clear();
        AgencyEntry.Text = "";
        UserEntry.Text = "";
        PassEntry.Text = "";
        GmailAddressEntry.Text = "";
        GmailAppPasswordEntry.Text = "";
#endif
    }

    private void OnResetWebSessionClicked(object sender, EventArgs e)
    {
#if WINDOWS
        // Stop kiosk first to avoid file locks
        OnStopClicked(sender, e);

        // Delete the WebView2 profile folder (cookies/session)
        //var folder = KioskWebViewHandler.UserDataFolderPath;
        //try
        //{
        //    if (Directory.Exists(folder))
        //        Directory.Delete(folder, recursive: true);
        //}
        //catch
        //{
        //    // If locked, you can retry after app restart; keeping silent here is fine for kiosk
        //}
#endif
    }
}
