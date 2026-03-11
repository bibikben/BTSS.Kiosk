using BTSS.IAR.Record.Models;

namespace BTSS.IAR.Api.Models
{
    internal static class UnitExtensions
    {
        public static DateTime? UpdatedAtFallback(this Unit unit) =>
            unit.Arrived
            ?? unit.Enroute
            ?? unit.Dispatched
            ?? unit.Cleared
            ?? unit.Quarters;
    }
}
