using System.Diagnostics;

namespace BTSS.Installer;

internal static class ServiceControl
{
    public static void InstallOrUpdate(string serviceExePath)
    {
        var serviceName = "BTSS.Service";
        var quotedExe = Quote(serviceExePath);

        if (ServiceExists(serviceName))
        {
            RunSc($"stop {serviceName}", ignoreErrors: true);
            RunSc($"config {serviceName} binPath= {quotedExe} start= auto obj= LocalSystem");
        }
        else
        {
            RunSc($"create {serviceName} binPath= {quotedExe} start= auto obj= LocalSystem DisplayName= \"BTSS Service\"");
        }

        RunSc($"description {serviceName} \"BTSS incident polling and print orchestration service\"");
        RunSc($"failure {serviceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000", ignoreErrors: true);
        RunSc($"start {serviceName}", ignoreErrors: true);
    }

    private static bool ServiceExists(string serviceName)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"query {serviceName}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        });

        process!.WaitForExit();
        return process.ExitCode == 0;
    }

    private static void RunSc(string arguments, bool ignoreErrors = false)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        });

        process!.WaitForExit();
        if (process.ExitCode == 0 || ignoreErrors)
            return;

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        throw new InvalidOperationException($"sc.exe {arguments} failed. {output} {error}".Trim());
    }

    private static string Quote(string value) => "\"" + value + "\"";
}
