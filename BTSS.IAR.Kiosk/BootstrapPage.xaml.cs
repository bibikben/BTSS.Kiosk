using BTSS.IAR.Kiosk.Services;

#if WINDOWS
using BTSS.IAR.Kiosk.Platforms.Windows;
#endif

namespace BTSS.IAR.Kiosk;

public partial class BootstrapPage : ContentPage
{
    private readonly List<string> _monitorLabels = new();
    private readonly List<KioskAgencyMembershipDto> _agencies = new();
    private bool _adminMode;
    private string? _lastLoginUserName;
    private string? _lastLoginPassword;

    public BootstrapPage()
    {
        InitializeComponent();
    }

    private App CurrentApp => (App)Application.Current!;
    private IKioskBootstrapService Bootstrap => CurrentApp.BootstrapService;
    private KioskAdminAuthService AdminAuth => Handler?.MauiContext?.Services.GetService<KioskAdminAuthService>() ?? new KioskAdminAuthService();

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        BootstrapCompatibilityStore.LoadIfNeeded();

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
        AdminUserNameEntry.Text = _lastLoginUserName ?? "superadmin";

        LoadMonitors();
        SetAdminMode(AdminAuth.HasAdminSession);
        UpdateAdminSummary();
        if (_adminMode)
        {
            await LoadExistingConfigurationAsync();
        }
        else
        {
            StatusLabel.Text = "Standard runtime mode is active. Enter admin mode to view device pairing and diagnostics.";
        }
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
        BootstrapCompatibilityStore.Save();
    }

    private async Task LoadExistingConfigurationAsync()
    {
        if (!_adminMode)
        {
            StatusLabel.Text = "Configuration lookup is hidden until admin mode is active.";
            return;
        }

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
                DiagnosticsLabel.Text = BuildDiagnosticsText(null);
                return;
            }

            ApplyResolvedConfiguration(config);
            StatusLabel.Text = "Assigned display configuration loaded from API.";
            DiagnosticsLabel.Text = BuildDiagnosticsText(config);
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Unable to load configuration: {ex.Message}";
            DiagnosticsLabel.Text = BuildDiagnosticsText(null, ex.Message);
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

    private void SetAdminMode(bool enabled)
    {
        _adminMode = enabled;
        ModeLabel.Text = enabled ? "Mode: Admin" : "Mode: Standard runtime";
        ExitAdminModeButton.IsVisible = enabled;
        AdminLoginSection.IsVisible = true;
        ApiPairingSection.IsVisible = enabled;
        ProfileSection.IsVisible = enabled;
        DiagnosticsSection.IsVisible = enabled;
        UpdateAdminSummary();
    }

    private void UpdateAdminSummary()
    {
        var session = AdminAuth.CurrentSession;
        if (!_adminMode || session is null)
        {
            AdminSummaryLabel.Text = "No admin session";
            return;
        }

        var agencyText = session.Agencies.FirstOrDefault(x => x.AgencyId == session.ActiveAgencyId)?.AgencyName
            ?? (session.ActiveAgencyId.HasValue ? $"Agency #{session.ActiveAgencyId}" : "No active agency");
        var permissionText = session.Permissions.Length == 0 ? "No explicit permissions" : string.Join(", ", session.Permissions.OrderBy(x => x));
        AdminSummaryLabel.Text = $"Logged in as {session.DisplayName} ({session.UserName}) | {(session.IsSuperUser ? "Super user" : "Admin")} | Active agency: {agencyText} | Permissions: {permissionText}";
    }

    private string BuildDiagnosticsText(DeviceResolvedConfigurationDto? config, string? error = null)
    {
        var lines = new List<string>
        {
            $"Saved API base URL: {AppSettings.IarApiBaseUrl}",
            $"Saved client ID: {AppSettings.IarApiClientId}",
            $"Saved startup URL: {AppSettings.SavedUrl}",
            $"Selected monitor index: {AppSettings.SelectedMonitorIndex}"
        };

        var session = AdminAuth.CurrentSession;
        if (session is not null)
        {
            lines.Add($"Admin user: {session.UserName}");
            lines.Add($"Is super user: {session.IsSuperUser}");
            lines.Add($"Active agency id: {(session.ActiveAgencyId?.ToString() ?? "(none)")}");
        }

        if (config is not null)
        {
            lines.Add($"Resolved API client id: {config.ApiClientId}");
            lines.Add($"Resolved agency id: {config.AgencyId}");
            lines.Add($"Resolved device id: {config.DeviceId}");
            lines.Add($"Resolved startup URL: {config.ResolveStartupUrl() ?? "(none)"}");
            lines.Add($"Resolved monitor index: {(config.ResolveMonitorIndex()?.ToString() ?? "(none)")}");
        }

        if (!string.IsNullOrWhiteSpace(error))
            lines.Add($"Last error: {error}");

        return string.Join(Environment.NewLine, lines);
    }

    private void BindAgencyPicker(KioskAdminSessionDto session)
    {
        _agencies.Clear();
        _agencies.AddRange(session.Agencies.OrderBy(x => x.AgencyName));
        AgencyPicker.ItemsSource = _agencies.Select(x => $"{x.AgencyName} ({x.AgencyCode})").ToList();

        var selectedAgencyId = session.ActiveAgencyId ?? _agencies.FirstOrDefault(x => x.IsDefault)?.AgencyId;
        if (selectedAgencyId.HasValue)
        {
            var index = _agencies.FindIndex(x => x.AgencyId == selectedAgencyId.Value);
            AgencyPicker.SelectedIndex = index;
        }
        else
        {
            AgencyPicker.SelectedIndex = _agencies.Count > 0 ? 0 : -1;
        }
    }

    private int? SelectedAgencyId => AgencyPicker.SelectedIndex >= 0 && AgencyPicker.SelectedIndex < _agencies.Count
        ? _agencies[AgencyPicker.SelectedIndex].AgencyId
        : null;

    private async Task<bool> EnsureAdminLoginAsync()
    {
        if (AdminAuth.HasAdminSession)
            return true;

        SetAdminMode(true);
        await DisplayAlert("Admin mode", "Sign in to unlock device registration, local settings, and diagnostics.", "OK");
        return false;
    }

    private async Task<bool> LoginAdminAsync(int? agencyId)
    {
        if (string.IsNullOrWhiteSpace(ApiBaseUrlEntry.Text))
        {
            await DisplayAlert("Missing API URL", "Enter the API base URL before attempting admin login.", "OK");
            return false;
        }

        var userName = (AdminUserNameEntry.Text ?? string.Empty).Trim();
        var password = AdminPasswordEntry.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Missing credentials", "Username and password are required for admin mode.", "OK");
            return false;
        }

        try
        {
            var session = await AdminAuth.LoginAsync(ApiBaseUrlEntry.Text.Trim(), userName, password, agencyId);
            if (session is null)
            {
                await DisplayAlert("Access denied", "The provided account does not have kiosk admin access.", "OK");
                return false;
            }

            _lastLoginUserName = userName;
            _lastLoginPassword = password;
            BindAgencyPicker(session);
            SetAdminMode(true);
            UpdateAdminSummary();
            StatusLabel.Text = $"Admin authenticated as {session.DisplayName}.";
            await LoadExistingConfigurationAsync();
            return true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Login failed", ex.Message, "OK");
            return false;
        }
    }

    private async void OnRunDisplayClicked(object sender, EventArgs e)
    {
        try
        {
            PersistApiSettings();
            var started = await CurrentApp.TryBootstrapAndStartDisplayAsync(showBootstrapIfMissingConfig: true);
            if (!started)
            {
                await DisplayAlert("No assignment found", "This device does not have an assigned display configuration yet. An administrator must register or update the device first.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Unable to start display", ex.Message, "OK");
        }
    }

    private void OnEnterAdminModeClicked(object sender, EventArgs e)
    {
        SetAdminMode(true);
        StatusLabel.Text = "Admin mode is visible. Sign in to unlock protected sections.";
    }

    private void OnExitAdminModeClicked(object sender, EventArgs e)
    {
        AdminAuth.Logout();
        AdminPasswordEntry.Text = string.Empty;
        _lastLoginPassword = null;
        _agencies.Clear();
        AgencyPicker.ItemsSource = null;
        AgencyPicker.SelectedIndex = -1;
        DiagnosticsLabel.Text = string.Empty;
        SetAdminMode(false);
        StatusLabel.Text = "Returned to standard runtime mode. Technical API details are hidden.";
    }

    private async void OnAdminLoginClicked(object sender, EventArgs e)
    {
        await LoginAdminAsync(SelectedAgencyId);
    }

    private async void OnApplyAgencyClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastLoginUserName) || string.IsNullOrWhiteSpace(_lastLoginPassword))
        {
            await DisplayAlert("No session", "Sign in first, then choose an agency to apply.", "OK");
            return;
        }

        AdminUserNameEntry.Text = _lastLoginUserName;
        AdminPasswordEntry.Text = _lastLoginPassword;
        await LoginAdminAsync(SelectedAgencyId);
    }

    private void OnAdminLogoutClicked(object sender, EventArgs e)
    {
        OnExitAdminModeClicked(sender, e);
    }

    private async void OnUseAssignedClicked(object sender, EventArgs e)
    {
        if (!await EnsureAdminLoginAsync())
            return;
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
        if (!await EnsureAdminLoginAsync())
            return;
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
            DiagnosticsLabel.Text = BuildDiagnosticsText(config);
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
        if (!await EnsureAdminLoginAsync())
            return;
        await LoadExistingConfigurationAsync();
    }

    private async void OnClearClicked(object sender, EventArgs e)
    {
        if (!await EnsureAdminLoginAsync())
            return;
        Bootstrap.ClearSavedApiSettings();
        BootstrapCompatibilityStore.Clear();
        ApiBaseUrlEntry.Text = AppSettings.IarApiBaseUrl;
        ApiClientIdEntry.Text = string.Empty;
        ApiClientSecretEntry.Text = string.Empty;
        DiagnosticsLabel.Text = BuildDiagnosticsText(null);
        StatusLabel.Text = "Saved API settings cleared for this device.";
        await DisplayAlert("Cleared", "Saved API settings were removed for this kiosk.", "OK");
    }
}
