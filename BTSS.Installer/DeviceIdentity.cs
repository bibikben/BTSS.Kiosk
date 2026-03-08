using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

namespace BTSS.Installer;

internal static class DeviceIdentity
{
    public static string GetMachinePermanentId()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var machineGuid = key?.GetValue("MachineGuid")?.ToString();
            if (!string.IsNullOrWhiteSpace(machineGuid))
                return machineGuid.Trim();
        }
        catch
        {
        }

        using var sha = SHA256.Create();
        var input = Encoding.UTF8.GetBytes($"{Environment.MachineName}|{Environment.UserDomainName}");
        return Convert.ToHexString(sha.ComputeHash(input));
    }

    public static string BuildDeviceId() => $"{Environment.MachineName}-{GetMachinePermanentId()[..12]}";
}
