using System.Diagnostics;
using BTSS.IAR.Models;

namespace BTSS.IAR.Services;

public sealed class WindowsServiceBridge
{
    public async Task<ServiceStatusInfo> GetStatusAsync(string serviceName = "BTSS Service")
    {
        try
        {
            var output = await RunScAsync($"query \"{serviceName}\"");
            return new ServiceStatusInfo
            {
                IsAvailable = true,
                Status = ParseStatus(output),
                Detail = output
            };
        }
        catch (Exception ex)
        {
            return new ServiceStatusInfo
            {
                IsAvailable = false,
                Status = "Unavailable",
                Detail = ex.Message
            };
        }
    }

    public Task<string> StartAsync(string serviceName = "BTSS Service") => RunScAsync($"start \"{serviceName}\"");
    public Task<string> StopAsync(string serviceName = "BTSS Service") => RunScAsync($"stop \"{serviceName}\"");

    private static async Task<string> RunScAsync(string arguments)
    {
        var psi = new ProcessStartInfo("sc.exe", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start sc.exe");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + Environment.NewLine + stderr;
    }

    private static string ParseStatus(string output)
    {
        if (output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase)) return "Running";
        if (output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase)) return "Stopped";
        if (output.Contains("START_PENDING", StringComparison.OrdinalIgnoreCase)) return "Start pending";
        if (output.Contains("STOP_PENDING", StringComparison.OrdinalIgnoreCase)) return "Stop pending";
        return "Unknown";
    }
}
