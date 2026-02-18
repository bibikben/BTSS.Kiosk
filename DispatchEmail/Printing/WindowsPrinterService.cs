using System.Text;
using BTSS.IAR.Kiosk.DispatchEmail.Reporting;

namespace BTSS.IAR.Kiosk.DispatchEmail.Printing;

public class WindowsPrinterService : IPrinterService
{
    public Task PrintAsync(string title, PivotTableResult table, string footer)
    {
#if WINDOWS
        return Task.Run(() =>
        {
            var text = BuildText(title, table, footer);

            using var pd = new System.Drawing.Printing.PrintDocument();
            pd.PrintPage += (_, e) =>
            {
                using var font = new System.Drawing.Font("Consolas", 10);
                e.Graphics.DrawString(text, font, System.Drawing.Brushes.Black, 20, 20);
            };
            pd.Print();
        });
#else
        throw new PlatformNotSupportedException("Printing is only implemented for Windows in this project.");
#endif
    }

    private static string BuildText(string title, PivotTableResult table, string footer)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine(new string('-', 100));

        int unitW = 10, groupW = 10, statusW = 10;

        sb.Append(Pad("Unit", unitW))
          .Append(Pad("Group", groupW));

        foreach (var c in table.StatusColumns)
            sb.Append(Pad(c, statusW));
        sb.AppendLine();

        sb.AppendLine(new string('-', 100));

        foreach (var r in table.Rows)
        {
            sb.Append(Pad(r.Unit, unitW))
              .Append(Pad(r.Group, groupW));

            foreach (var c in table.StatusColumns)
            {
                r.StatusToTime.TryGetValue(c, out var v);
                sb.Append(Pad(v ?? "", statusW));
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine(footer);
        return sb.ToString();
    }

    private static string Pad(string s, int width)
    {
        s ??= "";
        if (s.Length > width - 1) s = s[..(width - 1)];
        return s.PadRight(width);
    }
}
