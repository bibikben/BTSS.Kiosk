using BTSS.IAR.Record.Models;
using BTSS.Service.Entities;
using BTSS.Service.Options;
using BTSS.Service.Services;
using Microsoft.Extensions.Options;

namespace BTSS.Service.Workers;

public sealed class SftpIncidentImportWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ServiceRuntimeOptions> options,
    ILogger<SftpIncidentImportWorker> logger) : BackgroundService
{
    private readonly ServiceRuntimeOptions _options = options.Value;
    private int _consecutiveFailures;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.SftpImport.Enabled)
        {
            logger.LogInformation("SFTP incident import is disabled.");
            return;
        }

        logger.LogInformation("SFTP incident import worker started for {Host}:{Port}{Directory}.", _options.SftpImport.Host, _options.SftpImport.Port, _options.SftpImport.RemoteDirectory);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var scanner = scope.ServiceProvider.GetRequiredService<SftpIncidentFileScanner>();
                var ledger = scope.ServiceProvider.GetRequiredService<SftpImportLedger>();
                var ingestClient = scope.ServiceProvider.GetRequiredService<SftpIncidentIngestClient>();

                var files = await scanner.ListFilesAsync(stoppingToken);
                logger.LogInformation("SFTP scan found {Count} candidate files.", files.Count);

                foreach (var file in files)
                {
                    var candidate = await scanner.ReadCandidateAsync(file, stoppingToken);
                    var shouldProcess = await ledger.ShouldProcessAsync(file, candidate.ContentHash, _options.SftpImport.ReprocessWhenRemoteFileChanges, stoppingToken);
                    var ledgerFile = await ledger.RecordSeenAsync(file, candidate.ContentHash, stoppingToken);

                    if (!shouldProcess)
                    {
                        logger.LogDebug("Skipping previously processed SFTP file {RemotePath}.", file.RemotePath);
                        continue;
                    }

                    if (candidate.Incidents.Count == 0)
                    {
                        await ledger.MarkFailedAsync(ledgerFile.Id, "No importable incidents were found in the JSON payload.", stoppingToken);
                        continue;
                    }

                    var posts = new List<BTSS.Service.Models.SftpPostResult>();
                    foreach (var incident in candidate.Incidents)
                    {
                        if (_options.SftpImport.PostToApi)
                        {
                            posts.Add(await ingestClient.PostAsync(incident, stoppingToken));
                        }
                        else
                        {
                            posts.Add(new BTSS.Service.Models.SftpPostResult(true, null, "Skipped API post by configuration.", null));
                        }
                    }

                    var incidentIds = candidate.Incidents
                        .Select(x => x.GetCallIdentifier() ?? string.Empty)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToArray();

                    await ledger.MarkProcessedAsync(ledgerFile.Id, posts, incidentIds, stoppingToken);

                    var failedCount = posts.Count(x => !x.Success);
                    if (failedCount > 0)
                    {
                        logger.LogWarning("Processed SFTP file {RemotePath} with {FailedCount} failed API post(s).", file.RemotePath, failedCount);
                    }
                    else
                    {
                        logger.LogInformation("Processed SFTP file {RemotePath} with {IncidentCount} incident(s).", file.RemotePath, incidentIds.Length);
                    }
                }

                _consecutiveFailures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                logger.LogError(ex, "SFTP incident import cycle failed.");
            }

            await Task.Delay(ResolveDelay(), stoppingToken);
        }
    }

    private TimeSpan ResolveDelay()
    {
        if (_consecutiveFailures < _options.MaxConsecutiveFailuresBeforeBackoff)
            return TimeSpan.FromSeconds(_options.SftpImport.PollIntervalSeconds);

        var exponent = Math.Min(_consecutiveFailures - _options.MaxConsecutiveFailuresBeforeBackoff + 1, 6);
        var minutes = Math.Min(Math.Pow(2, exponent), _options.MaxBackoffMinutes);
        return TimeSpan.FromMinutes(minutes);
    }
}
