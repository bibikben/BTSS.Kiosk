using BTSS.IAR.Kiosk.DispatchEmail.Printing;
using BTSS.IAR.Kiosk.DispatchEmail.Reporting;
using BTSS.IAR.Kiosk.Services;
using BTSS.IAR.Kiosk.Services.DispatchEmail;
using BTSS.IAR.Record.Models;

namespace BTSS.IAR.Kiosk.Services.IarApi;

public interface IIarPollingService
{
    void Start();
    void Stop();
}

/// <summary>
/// Polls the BTSS.IAR.Api every minute for newly-closed call records.
/// When a close is detected it fetches the full call record and prints a pivot report.
/// </summary>
public sealed class IarApiPollingService : IIarPollingService
{
    private readonly IIarApiClient _api;
    private readonly IIarPivotReportBuilder _pivot;
    private readonly IPrintService _printer;

    private readonly TimeSpan _pollEvery = TimeSpan.FromMinutes(1);
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public IarApiPollingService(IIarApiClient api, IIarPivotReportBuilder pivot, IPrintService printer)
    {
        _api = api;
        _pivot = pivot;
        _printer = printer;
    }

    public void Start()
    {
        if (_loop != null && !_loop.IsCompleted) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _loop = null;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_pollEvery);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CheckOnceAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IAR POLL] {ex}");
            }

            try { await timer.WaitForNextTickAsync(ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckOnceAsync(CancellationToken ct)
    {
        if (AppSettings.PauseChecking)
            return;
        if (!string.Equals(AppSettings.ProcessingMode, "IarApi", StringComparison.OrdinalIgnoreCase))
            return;

        var agencyId = AppSettings.IarApiAgencyId;
        if (agencyId <= 0) return;

        var closings = await _api.CheckForCloseAsync(agencyId, ct);
        foreach (var callId in closings)
        {
            ct.ThrowIfCancellationRequested();
            var record = await _api.GetCallRecordAsync(agencyId, callId, ct);
            if (record == null) continue;

            await PrintRecordAsync(record);
        }
    }

    private async Task PrintRecordAsync(EmergencyCallUnified record)
    {
        PivotTableResult pivot = _pivot.Build(record);
        var id = record.GetCallIdentifier() ?? "(unknown)";
        var type = record.GetCallType() ?? "";
        var addr = record.GetAddress() ?? "";
        var updated = record.GetUpdatedAtUtc();

        var title = $"IAR Close Record  {(updated?.ToLocalTime().ToString("MM-dd-yyyy HH:mm:ss") ?? "")}  ID: {id}";
        var footer = $"Type: {type}  Address: {addr}";

        await _printer.PrintAsync(title, pivot, footer);
    }
}
