using BTSS.Service.Entities;
using BTSS.Service.Models;
using BTSS.Service.Options;
using BTSS.Service.Services;
using Microsoft.Extensions.Options;

namespace BTSS.Service.Workers;

public sealed class CallPollingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ServiceRuntimeOptions> options,
    ILogger<CallPollingWorker> logger) : BackgroundService
{
    private readonly ServiceRuntimeOptions _options = options.Value;
    private int _consecutiveFailures;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(_options.LocalDataDirectory);
        Directory.CreateDirectory(_options.ResolveHealthLogDirectory());

        while (!stoppingToken.IsCancellationRequested)
        {
            var startedAt = DateTimeOffset.UtcNow;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var pollingClient = scope.ServiceProvider.GetRequiredService<IncidentPollingClient>();
                var syncClient = scope.ServiceProvider.GetRequiredService<DeviceSyncClient>();
                var store = scope.ServiceProvider.GetRequiredService<IncidentStore>();
                var localSync = scope.ServiceProvider.GetRequiredService<LocalSyncStore>();
                var writer = scope.ServiceProvider.GetRequiredService<PrintJobWriter>();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IPrintDispatcher>();

                await syncClient.RegisterDeviceAsync(stoppingToken);
                await syncClient.SendHeartbeatAsync("running", "poll cycle starting", stoppingToken);

                var lastChange = await localSync.GetLastChangeUtcAsync(_options.ResolveDeviceId(), stoppingToken);
                if (!lastChange.HasValue)
                {
                    var bootstrap = await syncClient.BootstrapAsync(null, stoppingToken);
                    if (bootstrap is not null)
                        await localSync.ApplyBootstrapAsync(_options.ResolveDeviceId(), bootstrap, stoppingToken);
                }
                else
                {
                    var delta = await syncClient.GetChangesAsync(lastChange, stoppingToken);
                    if (delta is not null)
                    {
                        await localSync.ApplyChangesAsync(_options.ResolveDeviceId(), delta, stoppingToken);
                        var ids = delta.Changes.Select(x => x.SyncLogId).ToArray();
                        if (ids.Length > 0)
                        {
                            await syncClient.AckAsync(new SyncAckEnvelope
                            {
                                DeviceId = _options.ResolveDeviceId(),
                                BatchId = delta.BatchId,
                                SyncLogIds = ids,
                                LastSyncLogId = ids.Max(),
                                Notes = "service-sync"
                            }, stoppingToken);
                            await localSync.MarkAckAsync(_options.ResolveDeviceId(), delta.BatchId, ids.Max(), stoppingToken);
                        }
                    }
                }

                var result = await pollingClient.PollAsync(stoppingToken);
                if (result.HttpStatusCode is < 200 or >= 300)
                {
                    _consecutiveFailures++;
                    await store.AddPollRunAsync(new PollRunEntity
                    {
                        StartedAtUtc = startedAt,
                        FinishedAtUtc = DateTimeOffset.UtcNow,
                        Succeeded = false,
                        HttpStatusCode = result.HttpStatusCode,
                        IncidentCount = 0,
                        PayloadBytes = result.PayloadBytes,
                        Endpoint = result.Endpoint,
                        Error = $"Polling failed with HTTP {result.HttpStatusCode}."
                    }, stoppingToken);

                    await WriteHealthFileAsync($"poll failed: {result.HttpStatusCode}", stoppingToken);
                    await Task.Delay(ResolveDelay(), stoppingToken);
                    continue;
                }

                var closeTransitions = await store.UpsertAsync(result.Incidents, stoppingToken);
                foreach (var incident in closeTransitions)
                {
                    var summary = IncidentSummaryFactory.Create(incident.Payload, incident.CanonicalJson);
                    var generated = await writer.WriteAsync(summary, PrintTemplateKinds.ClosedSummary, stoppingToken);
                    var ledger = await store.CreatePrintLedgerEntryAsync(incident, generated.OutputPath, generated.Body, PrintTemplateKinds.ClosedSummary, stoppingToken);
                    var dispatch = await dispatcher.DispatchAsync(ledger, stoppingToken);
                    await store.MarkPrintCompletedAsync(ledger, dispatch.Status, dispatch.Error, stoppingToken);
                }

                var pending = await store.GetPendingPrintJobsAsync(stoppingToken);
                foreach (var job in pending.Where(x => x.AttemptCount < _options.MaxPrintAttempts))
                {
                    var dispatch = await dispatcher.DispatchAsync(job, stoppingToken);
                    await store.MarkPrintCompletedAsync(job, dispatch.Status, dispatch.Error, stoppingToken);
                }

                _consecutiveFailures = 0;
                await store.AddPollRunAsync(new PollRunEntity
                {
                    StartedAtUtc = startedAt,
                    FinishedAtUtc = DateTimeOffset.UtcNow,
                    Succeeded = true,
                    HttpStatusCode = result.HttpStatusCode,
                    IncidentCount = result.Incidents.Count,
                    PayloadBytes = result.PayloadBytes,
                    Endpoint = result.Endpoint
                }, stoppingToken);
                await syncClient.SendHeartbeatAsync("ok", $"poll ok: {result.Incidents.Count} incidents", stoppingToken);
                await WriteHealthFileAsync($"poll ok: {result.Incidents.Count} incidents", stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                logger.LogError(ex, "Call polling cycle failed.");
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var syncClient = scope.ServiceProvider.GetRequiredService<DeviceSyncClient>();
                    await syncClient.SendHeartbeatAsync("error", ex.Message, stoppingToken);
                }
                catch { }
                await WriteHealthFileAsync($"poll exception: {ex.Message}", stoppingToken);
            }

            await Task.Delay(ResolveDelay(), stoppingToken);
        }
    }

    private TimeSpan ResolveDelay()
    {
        if (_consecutiveFailures < _options.MaxConsecutiveFailuresBeforeBackoff)
            return TimeSpan.FromSeconds(_options.PollIntervalSeconds);

        var exponent = Math.Min(_consecutiveFailures - _options.MaxConsecutiveFailuresBeforeBackoff + 1, 6);
        var minutes = Math.Min(Math.Pow(2, exponent), _options.MaxBackoffMinutes);
        return TimeSpan.FromMinutes(minutes);
    }

    private async Task WriteHealthFileAsync(string message, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_options.ResolveHealthLogDirectory(), $"{DateTime.UtcNow:yyyyMMdd}.log");
        var line = $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}";
        await File.AppendAllTextAsync(path, line, cancellationToken);
    }
}
