using System.Drawing;
using System.Windows.Forms;

namespace BTSS.Installer;

internal sealed class MainForm : Form
{
    private readonly CheckBox _displayCheck = new() { Text = "Install Display/Admin (BTSS.IAR)", Checked = true, AutoSize = true };
    private readonly CheckBox _kioskCheck = new() { Text = "Install Kiosk (BTSS.IAR.Kiosk)", Checked = true, AutoSize = true };
    private readonly CheckBox _serviceCheck = new() { Text = "Install Service (BTSS.Service)", Checked = true, AutoSize = true };
    private readonly TextBox _payloadRoot = new() { Width = 520 };
    private readonly TextBox _installRoot = new() { Width = 520 };
    private readonly TextBox _apiBaseUrl = new() { Width = 300 };
    private readonly TextBox _clientId = new() { Width = 220 };
    private readonly TextBox _clientSecret = new() { Width = 220, UseSystemPasswordChar = true };
    private readonly TextBox _scope = new() { Width = 520 };
    private readonly TextBox _machineId = new() { Width = 520, ReadOnly = true };
    private readonly TextBox _deviceId = new() { Width = 520 };
    private readonly TextBox _displayName = new() { Width = 220 };
    private readonly TextBox _location = new() { Width = 220 };
    private readonly TextBox _stationCode = new() { Width = 120 };
    private readonly TextBox _stationName = new() { Width = 220 };
    private readonly TextBox _startupUrl = new() { Width = 520 };
    private readonly TextBox _printerName = new() { Width = 220 };
    private readonly NumericUpDown _pollInterval = new() { Minimum = 15, Maximum = 3600, Value = 60, Width = 90 };
    private readonly CheckBox _shellPrinting = new() { Text = "Enable shell printing", AutoSize = true };
    private readonly ComboBox _displaySelector = new() { Width = 520, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListBox _displayList = new() { Width = 520, Height = 100 };
    private readonly TextBox _log = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Width = 760, Height = 180, ReadOnly = true };
    private readonly Button _installButton = new() { Text = "Install", Width = 160, Height = 36 };
    private readonly Button _browsePayloadButton = new() { Text = "Browse...", Width = 90 };
    private readonly Button _browseInstallButton = new() { Text = "Browse...", Width = 90 };

    private readonly InstallWorkflow _workflow = new();
    private readonly List<DisplayInfoModel> _displays = new();

    public MainForm()
    {
        Text = "BTSS Installer";
        Width = 820;
        Height = 960;
        StartPosition = FormStartPosition.CenterScreen;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "favicon.ico");
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        _payloadRoot.Text = AppContext.BaseDirectory;
        _installRoot.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BTSS");
        _apiBaseUrl.Text = "https://localhost:56800/";
        _scope.Text = "clients.read clients.write global-settings.read global-settings.write device-settings.read device-settings.write display.read display.write kiosk.commands service.poll";
        _machineId.Text = $"{Environment.MachineName} / {DeviceIdentity.GetMachinePermanentId()}";
        _deviceId.Text = DeviceIdentity.BuildDeviceId();
        _displayName.Text = Environment.MachineName;
        _startupUrl.Text = "https://auth.iamresponding.com/login/member";

        BuildUi();
        LoadDisplays();

        _browsePayloadButton.Click += (_, _) => BrowseFolder(_payloadRoot);
        _browseInstallButton.Click += (_, _) => BrowseFolder(_installRoot);
        _installButton.Click += async (_, _) => await InstallAsync();
    }

    private void BuildUi()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(14)
        };

        var banner = BuildBanner();
        if (banner is not null)
            panel.Controls.Add(banner);

        panel.Controls.Add(Section("Components", _displayCheck, _kioskCheck, _serviceCheck));
        panel.Controls.Add(Section("Payload Source", Row(Labeled("Payload root", _payloadRoot), _browsePayloadButton), Row(Labeled("Install root", _installRoot), _browseInstallButton)));
        panel.Controls.Add(Section("API / OAuth", Row(Labeled("API base URL", _apiBaseUrl), Labeled("Client ID", _clientId), Labeled("Client secret", _clientSecret)), Labeled("Scopes", _scope)));
        panel.Controls.Add(Section("Device Identity", Labeled("Machine / permanent id", _machineId), Labeled("Installer device id", _deviceId)));
        panel.Controls.Add(Section("Display / Kiosk", Row(Labeled("Display name", _displayName), Labeled("Location", _location)), Row(Labeled("Station code", _stationCode), Labeled("Station name", _stationName)), Labeled("Startup URL", _startupUrl), Labeled("Assigned monitor", _displaySelector), Labeled("Detected displays", _displayList)));
        panel.Controls.Add(Section("Service", Row(Labeled("Printer name", _printerName), Labeled("Poll seconds", _pollInterval), _shellPrinting)));
        panel.Controls.Add(_installButton);
        panel.Controls.Add(Section("Install log", _log));

        Controls.Add(panel);
    }

    private Control? BuildBanner()
    {
        var splashPath = Path.Combine(AppContext.BaseDirectory, "Assets", "BTSSSplash.png");
        if (!File.Exists(splashPath))
            return null;

        var picture = new PictureBox
        {
            Width = 770,
            Height = 180,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(3, 3, 3, 12)
        };

        using var stream = File.OpenRead(splashPath);
        picture.Image = Image.FromStream(stream);

        return picture;
    }

    private static GroupBox Section(string title, params Control[] controls)
    {
        var box = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12), Width = 770 };
        var inner = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
        foreach (var control in controls) inner.Controls.Add(control);
        box.Controls.Add(inner);
        return box;
    }

    private static Control Labeled(string label, Control control)
    {
        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(4) };
        panel.Controls.Add(new Label { Text = label, AutoSize = true });
        panel.Controls.Add(control);
        return panel;
    }

    private static Control Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };
        foreach (var control in controls) row.Controls.Add(control);
        return row;
    }

    private void LoadDisplays()
    {
        _displays.Clear();
        _displaySelector.Items.Clear();
        _displayList.Items.Clear();

        for (var i = 0; i < Screen.AllScreens.Length; i++)
        {
            var screen = Screen.AllScreens[i];
            var model = new DisplayInfoModel
            {
                Index = i,
                DeviceName = screen.DeviceName,
                Resolution = $"{screen.Bounds.Width}x{screen.Bounds.Height}",
                Primary = screen.Primary,
                Bounds = $"{screen.Bounds.X},{screen.Bounds.Y},{screen.Bounds.Width},{screen.Bounds.Height}"
            };
            _displays.Add(model);
            _displaySelector.Items.Add(model);
            _displayList.Items.Add(model.ToString());
        }

        if (_displaySelector.Items.Count > 0)
            _displaySelector.SelectedIndex = _displays.FindIndex(x => x.Primary) is var idx && idx >= 0 ? idx : 0;
    }

    private static void BrowseFolder(TextBox textBox)
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = textBox.Text, UseDescriptionForTitle = true, Description = "Select folder" };
        if (dialog.ShowDialog() == DialogResult.OK)
            textBox.Text = dialog.SelectedPath;
    }

    private async Task InstallAsync()
    {
        try
        {
            ToggleBusy(true);
            _log.Clear();
            Log("Starting installation...");

            var plan = BuildPlan();
            var progress = new Progress<string>(Log);
            var result = await _workflow.ExecuteAsync(plan, _displays, progress, CancellationToken.None);

            foreach (var message in result.Messages) Log(message);
            foreach (var warning in result.Warnings) Log("WARNING: " + warning);

            Log("Installation complete.");
            MessageBox.Show(this, result.Warnings.Count == 0 ? "Installation complete." : "Installation completed with warnings. Review the log.", "BTSS Installer", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            MessageBox.Show(this, ex.ToString(), "Install failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleBusy(false);
        }
    }

    private InstallPlan BuildPlan()
    {
        if (!_displayCheck.Checked && !_kioskCheck.Checked && !_serviceCheck.Checked)
            throw new InvalidOperationException("Select at least one component.");
        if (string.IsNullOrWhiteSpace(_clientId.Text) || string.IsNullOrWhiteSpace(_clientSecret.Text))
            throw new InvalidOperationException("Client ID and client secret are required.");
        if (_displaySelector.SelectedItem is not DisplayInfoModel selectedDisplay)
            throw new InvalidOperationException("Select a target display.");

        return new InstallPlan
        {
            InstallDisplayAdmin = _displayCheck.Checked,
            InstallKiosk = _kioskCheck.Checked,
            InstallService = _serviceCheck.Checked,
            PayloadRoot = _payloadRoot.Text.Trim(),
            InstallRoot = _installRoot.Text.Trim(),
            ApiBaseUrl = _apiBaseUrl.Text.Trim(),
            ClientId = _clientId.Text.Trim(),
            ClientSecret = _clientSecret.Text,
            Scope = _scope.Text.Trim(),
            DeviceId = _deviceId.Text.Trim(),
            MachineName = Environment.MachineName,
            MachinePermanentId = DeviceIdentity.GetMachinePermanentId(),
            DisplayName = _displayName.Text.Trim(),
            Location = _location.Text.Trim(),
            StationCode = _stationCode.Text.Trim(),
            StationName = _stationName.Text.Trim(),
            StartupUrl = _startupUrl.Text.Trim(),
            SelectedMonitorIndex = selectedDisplay.Index,
            PrinterName = _printerName.Text.Trim(),
            PollIntervalSeconds = (int)_pollInterval.Value,
            EnableShellPrinting = _shellPrinting.Checked
        };
    }

    private void Log(string message)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void ToggleBusy(bool isBusy)
    {
        UseWaitCursor = isBusy;
        _installButton.Enabled = !isBusy;
        foreach (Control control in Controls)
            control.Enabled = !isBusy || control == _log;
        _log.Enabled = true;
    }
}