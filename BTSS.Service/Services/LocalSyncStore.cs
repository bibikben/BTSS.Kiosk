using System.Text.Json;
using BTSS.IAR.Record.Models;
using BTSS.Service.Data;
using BTSS.Service.Entities;
using BTSS.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace BTSS.Service.Services;

public sealed class LocalSyncStore(ServiceDbContext db)
{
    public async Task<DateTimeOffset?> GetLastChangeUtcAsync(string deviceId, CancellationToken cancellationToken)
        => await db.LocalSyncStates.Where(x => x.DeviceId == deviceId).Select(x => x.LastChangeAtUtc).FirstOrDefaultAsync(cancellationToken);

    public async Task ApplyBootstrapAsync(string deviceId, SyncBootstrapDocument document, CancellationToken cancellationToken)
    {
        foreach (var incident in document.Incidents)
            await UpsertIncidentAsync(incident, cancellationToken);

        foreach (var item in document.Comments)
            await UpsertCommentAsync(item, cancellationToken);

        foreach (var item in document.Units)
            await UpsertUnitAsync(item, cancellationToken);

        foreach (var item in document.Timeline)
            await UpsertTimelineAsync(item, cancellationToken);

        await UpdateStateAsync(deviceId, state =>
        {
            state.LastBootstrapAtUtc = DateTimeOffset.UtcNow;
            state.LastChangeAtUtc = document.ServerUtc;
            state.LastBatchId = document.BatchId;
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyChangesAsync(string deviceId, SyncChangesDocument document, CancellationToken cancellationToken)
    {
        foreach (var change in document.Changes ?? [])
        {
            if (change.Incident.ValueKind != JsonValueKind.Undefined && change.Incident.ValueKind != JsonValueKind.Null)
                await UpsertIncidentAsync(change.Incident, cancellationToken);
            foreach (var comment in change.Comments ?? [])
                await UpsertCommentAsync(comment, cancellationToken);
            foreach (var unit in change.Units ?? [])
                await UpsertUnitAsync(unit, cancellationToken);
            foreach (var timeline in change.Timeline ?? [])
                await UpsertTimelineAsync(timeline, cancellationToken);
        }

        await UpdateStateAsync(deviceId, state =>
        {
            state.LastChangeAtUtc = document.ServerUtc;
            state.LastBatchId = document.BatchId;
            state.LastSyncLogId = document.Changes?.Select(x => (long?)x.SyncLogId).Max();
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAckAsync(string deviceId, string? batchId, long? lastSyncLogId, CancellationToken cancellationToken)
    {
        await UpdateStateAsync(deviceId, state =>
        {
            state.LastAckAtUtc = DateTimeOffset.UtcNow;
            state.LastBatchId = batchId ?? state.LastBatchId;
            state.LastSyncLogId = lastSyncLogId ?? state.LastSyncLogId;
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateStateAsync(string deviceId, Action<LocalSyncStateEntity> apply, CancellationToken cancellationToken)
    {
        var state = await db.LocalSyncStates.FirstOrDefaultAsync(x => x.DeviceId == deviceId, cancellationToken);
        if (state is null)
        {
            state = new LocalSyncStateEntity { DeviceId = deviceId, ConflictPolicy = "server-wins" };
            db.LocalSyncStates.Add(state);
        }
        apply(state);
    }

    private async Task UpsertIncidentAsync(JsonElement incidentElement, CancellationToken cancellationToken)
    {
        var raw = incidentElement.GetRawText();
        var model = EmergencyCallUnifiedJson.Deserialize(raw);
        if (model is null) return;
        var incidentId = ResolveIncidentId(model);
        var entity = await db.Incidents.FirstOrDefaultAsync(x => x.IncidentId == incidentId, cancellationToken);
        if (entity is null)
        {
            entity = new IncidentSnapshotEntity { IncidentId = incidentId, FirstSeenUtc = DateTimeOffset.UtcNow };
            db.Incidents.Add(entity);
        }
        entity.Agency = model.Details?.Agency ?? string.Empty;
        entity.Address = model.Headers?.Address ?? string.Empty;
        entity.CallType = model.Headers?.Type ?? string.Empty;
        entity.Status = model.Details?.Status ?? string.Empty;
        entity.IsClosed = model.Details?.Closed ?? false;
        entity.SourceUpdatedAtUtc = model.Details?.UpdatedAt;
        entity.LastSeenUtc = DateTimeOffset.UtcNow;
        entity.CanonicalJson = raw;
        entity.SummaryText = raw;
    }

    private async Task UpsertCommentAsync(JsonElement item, CancellationToken cancellationToken)
    {
        var incidentId = item.TryGetProperty("incidentId", out var iid) ? iid.GetInt64().ToString() : string.Empty;
        var message = item.TryGetProperty("message", out var msg) ? msg.GetString() ?? string.Empty : string.Empty;
        var occurred = item.TryGetProperty("occurredAtUtc", out var dt) && dt.ValueKind == JsonValueKind.String ? DateTime.Parse(dt.GetString()!) : (DateTime?)null;
        if (string.IsNullOrWhiteSpace(incidentId) || string.IsNullOrWhiteSpace(message)) return;
        var exists = await db.LocalIncidentComments.AnyAsync(x => x.IncidentId == incidentId && x.Message == message && x.OccurredAtUtc == occurred, cancellationToken);
        if (!exists)
            db.LocalIncidentComments.Add(new LocalIncidentCommentEntity { IncidentId = incidentId, Message = message, OccurredAtUtc = occurred, CreatedBy = item.TryGetProperty("createdBy", out var cb) ? cb.GetString() : null, CreatedAgency = item.TryGetProperty("createdAgency", out var ca) ? ca.GetString() : null });
    }

    private async Task UpsertUnitAsync(JsonElement item, CancellationToken cancellationToken)
    {
        var incidentId = item.TryGetProperty("incidentId", out var iid) ? iid.GetInt64().ToString() : string.Empty;
        var unitIdentifier = item.TryGetProperty("unitIdentifier", out var uid) ? uid.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(incidentId) || string.IsNullOrWhiteSpace(unitIdentifier)) return;
        var entity = await db.LocalIncidentUnits.FirstOrDefaultAsync(x => x.IncidentId == incidentId && x.UnitIdentifier == unitIdentifier, cancellationToken);
        if (entity is null)
        {
            entity = new LocalIncidentUnitEntity { IncidentId = incidentId, UnitIdentifier = unitIdentifier };
            db.LocalIncidentUnits.Add(entity);
        }
        entity.AgencyId = item.TryGetProperty("agencyId", out var aid) && aid.ValueKind == JsonValueKind.Number ? aid.GetInt32() : null;
        entity.Station = item.TryGetProperty("station", out var st) ? st.GetString() : null;
        entity.UnitType = item.TryGetProperty("unitType", out var ut) ? ut.GetString() : null;
        entity.CurrentStatus = item.TryGetProperty("currentStatus", out var cs) ? cs.GetString() : null;
    }

    private async Task UpsertTimelineAsync(JsonElement item, CancellationToken cancellationToken)
    {
        var incidentId = item.TryGetProperty("incidentId", out var iid) ? iid.GetInt64().ToString() : string.Empty;
        var unitIdentifier = item.TryGetProperty("unitIdentifier", out var uid) ? uid.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(incidentId) || string.IsNullOrWhiteSpace(unitIdentifier)) return;
        var entity = await db.LocalUnitTimelineFacts.FirstOrDefaultAsync(x => x.IncidentId == incidentId && x.UnitIdentifier == unitIdentifier, cancellationToken);
        if (entity is null)
        {
            entity = new LocalUnitTimelineFactEntity { IncidentId = incidentId, UnitIdentifier = unitIdentifier };
            db.LocalUnitTimelineFacts.Add(entity);
        }
        entity.AgencyId = item.TryGetProperty("agencyId", out var aid) && aid.ValueKind == JsonValueKind.Number ? aid.GetInt32() : null;
        entity.DispatchedAtUtc = ReadDate(item, "dispatchedAtUtc");
        entity.EnrouteAtUtc = ReadDate(item, "enrouteAtUtc");
        entity.ArrivedAtUtc = ReadDate(item, "arrivedAtUtc");
        entity.TransportBeginAtUtc = ReadDate(item, "transportBeginAtUtc");
        entity.TransportCompleteAtUtc = ReadDate(item, "transportCompleteAtUtc");
        entity.ClearedAtUtc = ReadDate(item, "clearedAtUtc");
        entity.InQuartersAtUtc = ReadDate(item, "inQuartersAtUtc");
    }

    private static string ResolveIncidentId(EmergencyCallUnified model)
        => model.Details?.Id ?? model.Id ?? Guid.NewGuid().ToString("N");

    private static DateTimeOffset? ToDto(string? value)
        => DateTimeOffset.TryParse(value, out var dto) ? dto : null;

    private static DateTime? ReadDate(JsonElement item, string name)
        => item.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && DateTime.TryParse(p.GetString(), out var dt) ? dt : null;
}
