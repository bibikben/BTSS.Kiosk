using BTSS.IAR.Kiosk.Services;

#if WINDOWS
using BTSS.IAR.Kiosk.Platforms.Windows;
#endif

namespace BTSS.IAR.Kiosk;

public partial class BootstrapPage : ContentPage
{
    private readonly List<string> _monitorLabels = new();

    public BootstrapPage()
    {
        InitializeComponent();
    }

    private App CurrentApp => (App)Application.Current!;
    private IKioskBootstrapService Bootstrap => CurrentApp.BootstrapService;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var device = Bootstrap.BuildLocalBootstrapInfo();
        DeviceIdLabel.Text = $"Device ID: {device.DeviceId}";
        MachineNameLabel.Text = $"Machine: {device.MachineName}";
        UserLabel.Text = $"User: {device.OsUser}";
        VersionLabel.Text = $"Version: {device.ApplicationVersion}";

        ApiBaseUrlEntry.Text = AppSettings.IarApiBaseUrl;
        ApiClientIdEntry.Text = AppSettings.IarApiClientId;
        ApiClientSecretEntry.Text = AppSettings.IarApiClientSecret;
        DisplayNameEntry.Text = AppSettings.KioskDisplayName ?? device.MachineName;
        LocationEntry.Text = AppSettings.KioskLocation;
        StationCodeEntry.Text = AppSettings.KioskStationCode;
        StationNameEntry.Text = AppSettings.KioskStationName;
        StartupUrlEntry.Text = AppSettings.SavedUrl;

        LoadMonitors();
        await LoadExistingConfigurationAsync();
    }

    private void LoadMonitors()
    {
        _monitorLabels.Clear();
#if WINDOWS
        var monitors = MonitorService.GetMonitors();
        foreach (var monitor in monitors.Select((m, i) => new { m, i }))
        {
            _monitorLabels.Add($"{monitor.i}: {monitor.m.DeviceName} {(monitor.m.IsPrimary ? "(Primary)" : string.Empty)} [{monitor.m.Width}x{monitor.m.Height}]");
        }
#else
        _monitorLabels.Add("0: Default display");
#endif
        MonitorPicker.ItemsSource = _monitorLabels;
        MonitorPicker.SelectedIndex = AppSettings.SelectedMonitorIndex >= 0 && AppSettings.SelectedMonitorIndex < _monitorLabels.Count
            ? AppSettings.SelectedMonitorIndex
            : (_monitorLabels.Count > 0 ? 0 : -1);
    }

    private void PersistApiSettings()
    {
        AppSettings.IarApiBaseUrl = (ApiBaseUrlEntry.Text ?? string.Empty).Trim();
        AppSettings.IarApiClientId = (ApiClientIdEntry.Text ?? string.Empty).Trim();
        AppSettings.IarApiClientSecret = ApiClientSecretEntry.Text ?? string.Empty;
        AppSettings.KioskDisplayName = (DisplayNameEntry.Text ?? string.Empty).Trim();
        AppSettings.KioskLocation = (LocationEntry.Text ?? string.Empty).Trim();
        AppSettings.KioskStationCode = (StationCodeEntry.Text ?? string.Empty).Trim();
        AppSettings.KioskStationName = (StationNameEntry.Text ?? string.Empty).Trim();
        AppSettings.SavedUrl = (StartupUrlEntry.Text ?? string.Empty).Trim();
        AppSettings.SelectedMonitorIndex = Math.Max(0, MonitorPicker.SelectedIndex);
    }

    private async Task LoadExistingConfigurationAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiBaseUrlEntry.Text) || string.IsNullOrWhiteSpace(ApiClientIdEntry.Text) || string.IsNullOrWhiteSpace(ApiClientSecretEntry.Text))
        {
            StatusLabel.Text = "Enter API connection details to load device assignment.";
            return;
        }

        try
        {
            PersistApiSettings();
            var config = await Bootstrap.TryGetConfigurationAsync();
            if (config is null)
            {
                StatusLabel.Text = "No assigned display configuration was found for this device yet.";
                return;
            }

            ApplyResolvedConfiguration(config);
            StatusLabel.Text = "Assigned display configuration loaded from API.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Unable to load configuration: {ex.Message}";
        }
    }

    private void ApplyResolvedConfiguration(DeviceResolvedConfigurationDto config)
    {
        DisplayNameEntry.Text = config.Profile?.DisplayName ?? DisplayNameEntry.Text;
        LocationEntry.Text = config.Profile?.Location ?? LocationEntry.Text;
        StationCodeEntry.Text = config.Profile?.StationCode ?? StationCodeEntry.Text;
        StationNameEntry.Text = config.Profile?.StationName ?? StationNameEntry.Text;
        StartupUrlEntry.Text = config.ResolveStartupUrl() ?? StartupUrlEntry.Text;

        var monitorIndex = config.ResolveMonitorIndex();
        if (monitorIndex.HasValue && monitorIndex.Value >= 0 && monitorIndex.Value < _monitorLabels.Count)
            MonitorPicker.SelectedIndex = monitorIndex.Value;

        PersistApiSettings();
    }

    private async void OnUseAssignedClicked(object sender, EventArgs e)
    {
        try
        {
            PersistApiSettings();
            var started = await CurrentApp.TryBootstrapAndStartDisplayAsync(showBootstrapIfMissingConfig: true);
            if (!started)
                await DisplayAlert("No assignment found", "This device does not have an assigned display configuration yet. Register or update the device first.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Unable to start display", ex.Message, "OK");
        }
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiBaseUrlEntry.Text) || string.IsNullOrWhiteSpace(ApiClientIdEntry.Text) || string.IsNullOrWhiteSpace(ApiClientSecretEntry.Text))
        {
            await DisplayAlert("Missing API settings", "API base URL, client ID, and client secret are required.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(StartupUrlEntry.Text))
        {
            await DisplayAlert("Missing startup URL", "A startup URL or display source is required for first-run registration.", "OK");
            return;
        }

        try
        {
            PersistApiSettings();
            var profile = new DeviceProfileDto
            {
                DisplayName = string.IsNullOrWhiteSpace(DisplayNameEntry.Text) ? Environment.MachineName : DisplayNameEntry.Text.Trim(),
                Location = string.IsNullOrWhiteSpace(LocationEntry.Text) ? null : LocationEntry.Text.Trim(),
                StationCode = string.IsNullOrWhiteSpace(StationCodeEntry.Text) ? null : StationCodeEntry.Text.Trim(),
                StationName = string.IsNullOrWhiteSpace(StationNameEntry.Text) ? null : StationNameEntry.Text.Trim(),
                StartupUrl = StartupUrlEntry.Text.Trim(),
                DisplaySource = StartupUrlEntry.Text.Trim(),
                Enabled = true
            };

            var config = await Bootstrap.RegisterDeviceAsync(profile, Math.Max(0, MonitorPicker.SelectedIndex));
            if (config is null)
            {
                await DisplayAlert("Registration failed", "The API did not return a device configuration.", "OK");
                return;
            }

            ApplyResolvedConfiguration(config);
            StatusLabel.Text = "Device registration updated and configuration retrieved from API.";
            await CurrentApp.StartDisplayAsync(config.ResolveStartupUrl() ?? StartupUrlEntry.Text.Trim(), config.ResolveMonitorIndex() ?? Math.Max(0, MonitorPicker.SelectedIndex));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Registration failed", ex.Message, "OK");
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadExistingConfigurationAsync();
    }

    private async void OnClearClicked(object sender, EventArgs e)
    {
        Bootstrap.ClearSavedApiSettings();
        ApiBaseUrlEntry.Text = AppSettings.IarApiBaseUrl;
        ApiClientIdEntry.Text = string.Empty;
        ApiClientSecretEntry.Text = string.Empty;
        StatusLabel.Text = "Saved API settings cleared for this device.";
        await DisplayAlert("Cleared", "Saved API settings were removed for this kiosk.", "OK");
    }
}
