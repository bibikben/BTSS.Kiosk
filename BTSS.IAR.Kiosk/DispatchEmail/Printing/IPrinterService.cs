using BTSS.IAR.Kiosk.DispatchEmail.Reporting;

namespace BTSS.IAR.Kiosk.DispatchEmail.Printing;

public interface IPrinterService
{
    Task PrintAsync(string title, PivotTableResult table, string footer);
}
