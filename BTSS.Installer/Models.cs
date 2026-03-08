using System.Text.Json;
using System.Text.Json.Nodes;

namespace BTSS.Installer;

internal sealed class InstallPlan
{
    public bool InstallDisplayAdmin { get; set; }
    public bool InstallKiosk { get; set; }
    public bool InstallService { get; set; }
    public string PayloadRoot { get; set; } = AppContext.BaseDirectory;
    public string InstallRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BTSS");
    public string ApiBaseUrl { get; set; } = "https://localhost:56800/";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = "clients.read clients.write global-settings.read global-settings.write device-settings.read device-settings.write display.read display.write kiosk.commands service.poll";
    public string DeviceId { get; set; } = string.Empty;
    public string MachineName { get; set; } = Environment.MachineName;
    public string MachinePermanentId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = Environment.MachineName;
    public string Location { get; set; } = string.Empty;
    public string StationCode { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public string StartupUrl { get; set; } = "https://auth.iamresponding.com/login/member";
    public int SelectedMonitorIndex { get; set; }
    public string PrinterName { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 60;
    public bool EnableShellPrinting { get; set; }

    public string DisplayInstallDir => Path.Combine(InstallRoot, "Display");
    public string KioskInstallDir => Path.Combine(InstallRoot, "Kiosk");
    public string ServiceInstallDir => Path.Combine(InstallRoot, "Service");
    public string CommonDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BTSS", "Service");
    public string LocalDisplayPrefsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BTSS.IAR", "admin-preferences.json");
    public string LocalKioskCompatDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BTSS.IAR.Kiosk");
    public string LocalKioskCompatPath => Path.Combine(LocalKioskCompatDir, "bootstrap-compat.json");
    public string ServiceConfigPath => Path.Combine(ServiceInstallDir, "appsettings.json");
}

internal sealed class DisplayInfoModel
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public bool Primary { get; set; }
    public string Bounds { get; set; } = string.Empty;
    public override string ToString() => $"{Index}: {DeviceName} ({Resolution})" + (Primary ? " [Primary]" : string.Empty);
}

internal sealed class InstallResult
{
    public List<string> Messages { get; } = new();
    public List<string> Warnings { get; } = new();
}

internal sealed class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
}

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
