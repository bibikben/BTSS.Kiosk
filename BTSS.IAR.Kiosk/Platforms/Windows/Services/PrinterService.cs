#if WINDOWS
using System.Drawing;
using System.Drawing.Printing;

namespace BTSS.IAR.Kiosk.Platforms.Windows.Services;

public interface ILocalPrinterService
{
    IReadOnlyList<string> GetInstalledPrinters();
    bool PrinterExists(string? printerName);
    string? GetSystemDefaultPrinter();
    string? ShowPrinterPicker(string? currentPrinterName = null);
    Task PrintTestPageAsync(string printerName, string title, CancellationToken ct = default);
}

public sealed class PrinterService : ILocalPrinterService
{
    public IReadOnlyList<string> GetInstalledPrinters()
        => PrinterSettings.InstalledPrinters.Cast<string>().OrderBy(x => x).ToList();

    public bool PrinterExists(string? printerName)
        => !string.IsNullOrWhiteSpace(printerName)
           && PrinterSettings.InstalledPrinters.Cast<string>()
               .Any(p => string.Equals(p, printerName, StringComparison.OrdinalIgnoreCase));

    public string? GetSystemDefaultPrinter()
    {
         var ps = new PrinterSettings();
        return string.IsNullOrWhiteSpace(ps.PrinterName) ? null : ps.PrinterName;
    }

    public string? ShowPrinterPicker(string? currentPrinterName = null)
    {
        if (PrinterExists(currentPrinterName))
            return currentPrinterName;

        var systemDefault = GetSystemDefaultPrinter();
        if (PrinterExists(systemDefault))
            return systemDefault;

        return GetInstalledPrinters().FirstOrDefault();
    }

    public Task PrintTestPageAsync(string printerName, string title, CancellationToken ct = default)
    {
        if (!PrinterExists(printerName))
            throw new InvalidOperationException("The selected printer is not installed on this machine.");

        using var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printerName;
        doc.DocumentName = title;
        doc.PrintPage += (_, e) =>
        {
            using var headingFont = new System.Drawing.Font("Segoe UI", 16, FontStyle.Bold);
            using var bodyFont = new System.Drawing.Font("Segoe UI", 10, FontStyle.Regular);
            var y = e.MarginBounds.Top;
            e.Graphics.DrawString(title, headingFont, Brushes.Black, e.MarginBounds.Left, y);
            y += 40;
            e.Graphics.DrawString($"Printer: {printerName}", bodyFont, Brushes.Black, e.MarginBounds.Left, y);
            y += 22;
            e.Graphics.DrawString($"Machine: {Environment.MachineName}", bodyFont, Brushes.Black, e.MarginBounds.Left, y);
            y += 22;
            e.Graphics.DrawString($"User: {Environment.UserName}", bodyFont, Brushes.Black, e.MarginBounds.Left, y);
            y += 22;
            e.Graphics.DrawString($"Printed: {DateTime.Now:F}", bodyFont, Brushes.Black, e.MarginBounds.Left, y);
            y += 32;
            e.Graphics.DrawString("BTSS kiosk printer test page.", bodyFont, Brushes.Black, e.MarginBounds.Left, y);
            e.HasMorePages = false;
        };

        doc.Print();
        return Task.CompletedTask;
    }
}
#endif
