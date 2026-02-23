using System.Collections;
using BTSS.IAR.Kiosk.Services;
using BTSS.IAR.Kiosk.Services.DispatchEmail;
using BTSS.IAR.Kiosk.Services.IarApi;
using Application = Microsoft.Maui.Controls.Application;

#if WINDOWS
using BTSS.IAR.Kiosk.Platforms.Windows;
using BTSS.IAR.Kiosk.Platforms.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Platform;
using WinRT.Interop;
#endif

namespace BTSS.IAR.Kiosk;

public partial class AdminPage : ContentPage
{
#if WINDOWS
    private List<MonitorInfo> _monitors = new();
    private GlobalHotKey? _hotKey;
    private bool _eventsWired;
    private IAutoStartService? _autoStart;
#endif

    private readonly App _app;
    private readonly IEmailCheckerService _emailChecker;
    private readonly IIarPollingService _iarPoller;
#if WINDOWS
    private readonly IPrinterService _printerService;
#endif

    public AdminPage(App app, IEmailCheckerService emailChecker, IIarPollingService iarPoller, IPrinterService printerService)
    {
        InitializeComponent();
        _app = app;
        _emailChecker = emailChecker;
        _iarPoller = iarPoller;

#if WINDOWS
        _printerService = printerService;
        var printers = _printerService.GetInstalledPrinters();
        PrinterPicker.ItemsSource = printers as IList;

        var selected = AppSettings.DefaultPrinterName ?? _printerService.GetSystemDefaultPrinter();
        if (!string.IsNullOrWhiteSpace(selected) && printers.Contains(selected))
            PrinterPicker.SelectedItem = selected;
#endif

        // Default tab
        SetTab("general");

        // Processing mode picker
        ProcessingModePicker.ItemsSource = new List<string> { "Email", "IarApi" };
        ProcessingModePicker.SelectedIndexChanged += (_, __) => ApplyProcessingModeVisibility();
    }

    private void ApplyProcessingModeVisibility()
    {
        var mode = ProcessingModePicker.SelectedItem as string ?? "Email";
        EmailFields.IsVisible = string.Equals(mode, "Email", StringComparison.OrdinalIgnoreCase);
        IarApiFields.IsVisible = string.Equals(mode, "IarApi", StringComparison.OrdinalIgnoreCase);
    }

    private void OnTabCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value) return;

        if (sender is RadioButton rb)
        {
            var tab = (rb.Value?.ToString() ?? "general").Trim().ToLowerInvariant();
            SetTab(tab);
        }
    }

    private void SetTab(string tab)
    {
        // Panels are defined in AdminPage.xaml
        GeneralPanel.IsVisible = tab == "general";
        DisplayPanel.IsVisible = tab == "display";
        EmailPanel.IsVisible = tab == "email";
        PrinterPanel.IsVisible = tab == "printer";
    }

    private async void OnSavePrinterClicked(object sender, EventArgs e)
    {
#if WINDOWS
        var selected = PrinterPicker.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selected))
        {
            await DisplayAlert("Printer", "Please select a printer.", "OK");
            return;
        }

        AppSettings.DefaultPrinterName = selected;
        await DisplayAlert("Printer", $"Saved: {selected}", "OK");
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

#if WINDOWS
        _autoStart ??=
            this.Handler?.MauiContext?.Services.GetService<IAutoStartService>()
            ?? Application.Current?.Windows.FirstOrDefault()?.Handler?.MauiContext?.Services.GetService<IAutoStartService>()
            ?? new AutoStartService();

        // Settings switches
        AutoStartSwitch.IsToggled = _autoStart.IsEnabled();
        StartDisplayOnStartupSwitch.IsToggled = AppSettings.StartDisplayOnStartup;
        StartMinimizedSwitch.IsToggled = AppSettings.StartMinimized;
        PauseCheckingSwitch.IsToggled = AppSettings.PauseChecking;

        if (!_eventsWired)
        {
            _eventsWired = true;

            AutoStartSwitch.Toggled += (_, ev) =>
            {
                _autoStart?.SetEnabled(ev.Value);
                AppSettings.AutoStartAtLogin = ev.Value;
            };

            StartDisplayOnStartupSwitch.Toggled += (_, ev) => AppSettings.StartDisplayOnStartup = ev.Value;
            StartMinimizedSwitch.Toggled += (_, ev) => AppSettings.StartMinimized = ev.Value;
            PauseCheckingSwitch.Toggled += (_, ev) => AppSettings.PauseChecking = ev.Value;
        }

        // Load saved creds
        var saved = await CredentialStore.LoadAsync();
        if (saved != null)
        {
            AgencyEntry.Text = saved.Agency;
            UserEntry.Text = saved.Username;
            PassEntry.Text = saved.Password;
        }

        // Load saved URL
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

        // Load saved Gmail creds
        var emailSaved = await DispatchEmailCredentialStore.LoadAsync();
        if (emailSaved != null)
        {
            GmailAddressEntry.Text = emailSaved.EmailAddress;
            GmailAppPasswordEntry.Text = emailSaved.AppPassword;
        }

        // Processing mode + IAR API settings
        var mode = AppSettings.ProcessingMode;
        ProcessingModePicker.SelectedItem = (string.Equals(mode, "IarApi", StringComparison.OrdinalIgnoreCase)) ? "IarApi" : "Email";
        IarApiBaseUrlEntry.Text = AppSettings.IarApiBaseUrl;
        IarApiAgencyIdEntry.Text = AppSettings.IarApiAgencyId.ToString();
        IarApiClientIdEntry.Text = AppSettings.IarApiClientId;
        IarApiClientSecretEntry.Text = AppSettings.IarApiClientSecret;
        ApplyProcessingModeVisibility();

        RegisterAdminHotKey();
#endif
    }

#if WINDOWS
    private void OnMonitorChanged(object? sender, EventArgs e)
    {
        if (MonitorPicker.SelectedIndex >= 0)
            AppSettings.SelectedMonitorIndex = MonitorPicker.SelectedIndex;
    }

    private void RegisterAdminHotKey()
    {
        if (_hotKey != null) return;

        var mauiWindow = this.Window;
        if (mauiWindow?.Handler?.PlatformView is not MauiWinUIWindow winuiWindow) return;

        var hwnd = WindowNative.GetWindowHandle(winuiWindow);

        // Ctrl+Shift+A
        const uint VK_A = 0x41;
        _hotKey = new GlobalHotKey(
            hwnd,
            id: 0xBEEF,
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
            PassEntry.Text ?? ""));

        await DispatchEmailCredentialStore.SaveAsync(new DispatchEmailCreds(
            GmailAddressEntry.Text ?? "",
            GmailAppPasswordEntry.Text ?? ""));

        // Save processing mode + API settings
        var mode = (ProcessingModePicker.SelectedItem as string ?? "Email").Trim();
        AppSettings.ProcessingMode = mode;
        AppSettings.IarApiBaseUrl = IarApiBaseUrlEntry.Text ?? AppSettings.IarApiBaseUrl;
        AppSettings.IarApiClientId = IarApiClientIdEntry.Text ?? "";
        AppSettings.IarApiClientSecret = IarApiClientSecretEntry.Text ?? "";
        if (int.TryParse(IarApiAgencyIdEntry.Text, out var agencyId))
            AppSettings.IarApiAgencyId = agencyId;

        // Start the appropriate pipeline
        _emailChecker.Stop();
        _iarPoller.Stop();
        if (string.Equals(mode, "IarApi", StringComparison.OrdinalIgnoreCase))
            _iarPoller.Start();
        else
        _emailChecker.Start();

        await _app.StartDisplayAsync(
            url: UrlEntry.Text ?? "",
            agency: AgencyEntry.Text ?? "",
            username: UserEntry.Text ?? "",
            password: PassEntry.Text ?? "",
            monitorIndex: MonitorPicker.SelectedIndex);
#endif
    }

    private void OnStopClicked(object sender, EventArgs e)
    {
        if (_app.DisplayWindow?.Page is DisplayPage dp)
            dp.StopWatchdog();

        _emailChecker.Stop();
        _iarPoller.Stop();
        _app.StopDisplayWindow();
    }

    private async void OnCallsClicked(object sender, EventArgs e)
    {
        try
        {
            var page = this.Handler?.MauiContext?.Services.GetService<CallsPage>();
            if (page != null)
                await Navigation.PushAsync(page);
        }
        catch { }
    }

    private async void OnAgenciesClicked(object sender, EventArgs e)
    {
        try
        {
            var page = this.Handler?.MauiContext?.Services.GetService<AgencySetupPage>();
            if (page != null)
                await Navigation.PushAsync(page);
        }
        catch { }
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
        //    // If locked, you can retry after app restart.
        //}
#endif
    }
}
