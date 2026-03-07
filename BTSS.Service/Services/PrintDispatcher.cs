using System.Diagnostics;
using BTSS.Service.Entities;
using BTSS.Service.Options;
using Microsoft.Extensions.Options;

namespace BTSS.Service.Services;

public sealed class PrintDispatcher(IOptions<ServiceRuntimeOptions> options, ILogger<PrintDispatcher> logger) : IPrintDispatcher
{
    private readonly ServiceRuntimeOptions _options = options.Value;

    public async Task<IReadOnlyList<string>> GetAvailablePrintersAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return [];

        try
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -Command \"Get-Printer | Select-Object -ExpandProperty Name\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process is null)
                return [];

            await process.WaitForExitAsync(cancellationToken);
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(stdout))
                return [];

            return stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to enumerate printers.");
            return [];
        }
    }

    public async Task<(string Status, string? Error)> DispatchAsync(PrintLedgerEntity entry, CancellationToken cancellationToken)
    {
        if (!_options.EnableShellPrinting)
            return ("queued", "Shell printing disabled; summary generated and queued in local storage.");

        if (!OperatingSystem.IsWindows())
            return ("queued", "Printing is only attempted on Windows hosts.");

        try
        {
            var printerName = string.IsNullOrWhiteSpace(entry.PrinterName) ? _options.PrinterName : entry.PrinterName;
            var command = string.IsNullOrWhiteSpace(printerName)
                ? $"notepad /p \"{entry.OutputPath}\""
                : $"print /d:\"{printerName}\" \"{entry.OutputPath}\"";

            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c {command}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });

            if (process is null)
                return ("failed", "Failed to start print process.");

            await process.WaitForExitAsync(cancellationToken);
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var error = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                logger.LogWarning("Print command failed for {Path}: {Error}", entry.OutputPath, error);
                return ("failed", string.IsNullOrWhiteSpace(error) ? "Unknown print failure." : error.Trim());
            }

            logger.LogInformation("Printed incident summary {Path} using template {TemplateKind}.", entry.OutputPath, entry.TemplateKind);
            return ("printed", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Print dispatch failed for {Path}.", entry.OutputPath);
            return ("failed", ex.Message);
        }
    }
}
