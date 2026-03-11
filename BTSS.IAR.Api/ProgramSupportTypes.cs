using System.Text.Json;
using System.Text.Json.Nodes;
using BTSS.IAR.Api.Models;
using BTSS.IAR.Record.Models;

namespace BTSS.IAR.Api;


file static class UnitExtensions
{
    public static DateTime? UpdatedAtFallback(this Unit unit) => unit.Arrived ?? unit.Enroute ?? unit.Dispatched ?? unit.Cleared ?? unit.Quarters;
}

static class JsonUtil
{
    public static readonly JsonSerializerOptions Options = EmergencyCallUnifiedJson.Options;
}

static class ScopeCatalog
{
    public const string ClientsRead = "clients.read";
    public const string ClientsWrite = "clients.write";
    public const string DisplayRead = "display.read";
    public const string DisplayWrite = "display.write";
    public const string DeviceRead = "device-settings.read";
    public const string DeviceWrite = "device-settings.write";
    public const string GlobalRead = "global-settings.read";
    public const string GlobalWrite = "global-settings.write";
    public const string KioskCommands = "kiosk.commands";
    public const string ServicePolling = "service.poll";
    public const string ReportAccess = "report.access";
    public const string Ingest = "call.ingest";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        ClientsRead,
        ClientsWrite,
        DisplayRead,
        DisplayWrite,
        DeviceRead,
        DeviceWrite,
        GlobalRead,
        GlobalWrite,
        KioskCommands,
        ServicePolling,
        ReportAccess,
        Ingest
    };

    public static readonly string[] DefaultClientScopes =
    {
        Ingest,
        DisplayRead,
        DisplayWrite,
        DeviceRead,
        DeviceWrite,
        GlobalRead,
        GlobalWrite,
        KioskCommands,
        ServicePolling,
        ReportAccess,
        ClientsRead,
        ClientsWrite
    };
}
