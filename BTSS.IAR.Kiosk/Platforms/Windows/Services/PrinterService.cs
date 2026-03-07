#if WINDOWS
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;

namespace BTSS.IAR.Kiosk.Platforms.Windows.Services;

public interface IPrinterService
{
    IReadOnlyList<string> GetInstalledPrinters();
    bool PrinterExists(string printerName);
    string? GetSystemDefaultPrinter();
}

public sealed class PrinterService : IPrinterService
{
    public IReadOnlyList<string> GetInstalledPrinters()
        => PrinterSettings.InstalledPrinters.Cast<string>().OrderBy(x => x).ToList();

    public bool PrinterExists(string printerName)
        => !string.IsNullOrWhiteSpace(printerName)
           && PrinterSettings.InstalledPrinters.Cast<string>().Any(p => string.Equals(p, printerName, StringComparison.OrdinalIgnoreCase));

    public string? GetSystemDefaultPrinter()
    {
        var ps = new PrinterSettings();
        return ps.PrinterName; // typically resolves to system default
    }
}
#endif