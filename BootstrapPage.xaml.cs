using BTSS.IAR.Kiosk.Services;
using BTSS.IAR.Record.Models;

#if WINDOWS
using BTSS.IAR.Kiosk.Platforms.Windows;
#endif

namespace BTSS.IAR.Kiosk;

public partial class BootstrapPage : ContentPage
{
    private readonly App _app;
    private readonly List<string> _monitorLabels = new();

    public BootstrapPage(App app)
    {
        InitializeComponent();
        _app = app;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var device = BuildDeviceBootstrapInfo();
        DeviceIdLabel.Text = $"Device ID: {device.DeviceId}";
        MachineNameLabel.Text = $"Machine: {device.MachineName}";
        UserLabel.Text = $"User: {device.OsUser}";
        VersionLabel.Text = $"Version: {device.ApplicationVersion}";

        UrlEntry.Text = AppSettings.SavedUrl;

        var saved = await CredentialStore.LoadAsync();
        if (saved != null)
        {
            AgencyEntry.Text = saved.Agency;
            UserEntry.Text = saved.Username;
            PassEntry.Text = saved.Password;
        }

        LoadMonitors();
    }

    private DeviceBootstrapInfo BuildDeviceBootstrapInfo()
    {
        var version = AppInfo.Current.VersionString;
        var deviceId = $"{Environment.MachineName}-{Environment.UserName}";

        return new DeviceBootstrapInfo(
            DeviceId: deviceId,
            MachineName: Environment.MachineName,
            OsUser: Environment.UserName,
            ApplicationName: AppInfo.Current.Name,
            ApplicationVersion: version,
            StartupUrl: AppSettings.SavedUrl,
            SelectedMonitorIndex: AppSettings.SelectedMonitorIndex,
            StationName: null,
            LocationName: null,
            IsPaired: false,
            CapturedAtUtc: DateTimeOffset.UtcNow);
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

    private async void OnStartDisplayClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UrlEntry.Text) || string.IsNullOrWhiteSpace(AgencyEntry.Text) || string.IsNullOrWhiteSpace(UserEntry.Text))
        {
            await DisplayAlert("Missing information", "URL, agency, and username are required before starting the display.", "OK");
            return;
        }

        AppSettings.SavedUrl = UrlEntry.Text.Trim();
        AppSettings.SelectedMonitorIndex = Math.Max(0, MonitorPicker.SelectedIndex);
        await CredentialStore.SaveAsync(new StoredCreds(AgencyEntry.Text.Trim(), UserEntry.Text.Trim(), PassEntry.Text ?? string.Empty));

        await _app.StartDisplayAsync(
            AppSettings.SavedUrl,
            AgencyEntry.Text.Trim(),
            UserEntry.Text.Trim(),
            PassEntry.Text ?? string.Empty,
            AppSettings.SelectedMonitorIndex);
    }

    private async void OnUseSavedClicked(object sender, EventArgs e)
    {
        await _app.TryStartDisplayFromSavedAsync(showAdminIfMissingConfig: true);
    }

    private async void OnClearClicked(object sender, EventArgs e)
    {
        CredentialStore.Clear();
        AgencyEntry.Text = string.Empty;
        UserEntry.Text = string.Empty;
        PassEntry.Text = string.Empty;
        await DisplayAlert("Cleared", "Stored kiosk credentials were removed for this device.", "OK");
    }
}
