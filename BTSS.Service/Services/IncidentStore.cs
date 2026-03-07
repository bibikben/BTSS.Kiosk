using System.Security.Cryptography;
using System.Text;
using BTSS.Service.Data;
using BTSS.Service.Entities;
using BTSS.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace BTSS.Service.Services;

public sealed class IncidentStore(ServiceDbContext db, ILogger<IncidentStore> logger)
{
    public async Task<IReadOnlyList<IncidentEnvelope>> UpsertAsync(IReadOnlyList<IncidentEnvelope> incidents, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var closeTransitions = new List<IncidentEnvelope>();

        foreach (var incident in incidents)
        {
            var existing = await db.Incidents.FirstOrDefaultAsync(x => x.IncidentId == incident.IncidentId, cancellationToken);
            var newHash = ComputeHash(incident.CanonicalJson);
            if (existing is null)
            {
                existing = new IncidentSnapshotEntity
                {
                    IncidentId = incident.IncidentId,
                    FirstSeenUtc = now
                };
                db.Incidents.Add(existing);
            }
            else if (existing.Status != incident.Status || existing.IsClosed != incident.IsClosed)
            {
                db.Transitions.Add(new IncidentTransitionEntity
                {
                    IncidentId = incident.IncidentId,
                    FromStatus = existing.Status,
                    ToStatus = incident.Status,
                    FromClosed = existing.IsClosed,
                    ToClosed = incident.IsClosed,
                    ObservedAtUtc = now,
                    Notes = existing.IsClosed == incident.IsClosed ? "status-change" : "close-transition"
                });
            }

            existing.Agency = incident.Agency;
            existing.Address = incident.Address;
            existing.CallType = incident.CallType;
            existing.Status = incident.Status;
            existing.IsClosed = incident.IsClosed;
            existing.SourceUpdatedAtUtc = incident.UpdatedAtUtc;
            existing.LastSeenUtc = now;
            existing.CanonicalJson = incident.CanonicalJson;
            existing.SummaryText = incident.SummaryText;
            existing.LastHash = newHash;

            if (incident.IsClosed)
            {
                var printedAlready = await db.PrintLedger.AnyAsync(
                    x => x.IncidentId == incident.IncidentId
                      && x.TemplateKind == PrintTemplateKinds.ClosedSummary
                      && x.SummaryHash == newHash,
                    cancellationToken);

                if (!printedAlready)
                    closeTransitions.Add(incident);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Stored {Count} incidents. {CloseCount} need print processing.", incidents.Count, closeTransitions.Count);
        return closeTransitions;
    }

    public Task AddPollRunAsync(PollRunEntity run, CancellationToken cancellationToken)
    {
        db.PollRuns.Add(run);
        return db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<PrintLedgerEntity>> GetPendingPrintJobsAsync(CancellationToken cancellationToken) =>
        db.PrintLedger
            .Where(x => x.PrintedAtUtc == null && x.AttemptCount < 10)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<PrintLedgerEntity> CreatePrintLedgerEntryAsync(IncidentEnvelope incident, string outputPath, string payloadSummary, string templateKind, CancellationToken cancellationToken)
    {
        var entry = new PrintLedgerEntity
        {
            IncidentId = incident.IncidentId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AttemptCount = 0,
            Status = "generated",
            TemplateKind = templateKind,
            PrinterName = null,
            OutputPath = outputPath,
            PayloadSummary = payloadSummary,
            SummaryHash = ComputeHash(incident.CanonicalJson)
        };
        db.PrintLedger.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task MarkPrintCompletedAsync(PrintLedgerEntity entry, string status, string? error, CancellationToken cancellationToken)
    {
        entry.Status = status;
        entry.Error = error;
        entry.AttemptCount += 1;
        entry.LastAttemptAtUtc = DateTimeOffset.UtcNow;
        entry.PrintedAtUtc = status is "printed" or "queued" ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
