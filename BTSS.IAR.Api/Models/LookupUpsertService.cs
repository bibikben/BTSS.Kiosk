using BTSS.IAR.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BTSS.IAR.Api.Models;

public interface IValueLookup
{
    string Value { get; set; }
}

public class CallTypeLookup : IValueLookup
{
    public int Id { get; set; }
    public string Value { get; set; } = "";
}

/// <summary>
/// Ensures lookup-table rows exist for incoming categorical values.
/// If a value does not exist, it is created.
/// </summary>
public sealed class LookupUpsertService
{
    private readonly AppDbContext _db;
    public LookupUpsertService(AppDbContext db) => _db = db;

    public async Task<short?> EnsureSourceSystemAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        // If seeded, this will already exist.
        var existing = await _db.SourceSystems.SingleOrDefaultAsync(x => x.Code == code);
        if (existing != null) return existing.Id;

        // If not seeded, create dynamically.
        var entity = new SourceSystemEntity { Code = code.Trim() };
        _db.SourceSystems.Add(entity);
        await _db.SaveChangesAsync();
        return entity.Id;
    }

    public Task<int?> EnsurePriorityAsync(string? value)
        => EnsureValueLookupAsync(_db, _db.Priorities, value, v => new PriorityEntity { Value = v });

    public Task<int?> EnsureCallTypeAsync(string? value)
        => EnsureValueLookupAsync(_db, _db.CallTypes, value, v => new CallTypeEntity { Value = v });

    public Task<int?> EnsureCallStatusAsync(string? value)
        => EnsureValueLookupAsync(_db, _db.CallStatuses, value, v => new CallStatusEntity { Value = v });

    public Task<int?> EnsureUnitStatusAsync(string? value)
        => EnsureValueLookupAsync(_db, _db.UnitStatuses, value, v => new UnitStatusEntity { Value = v });

    public async Task<int?> EnsureCadAgencyAsync(string? externalKey)
    {
        if (string.IsNullOrWhiteSpace(externalKey)) return null;
        var key = externalKey.Trim();

        var existing = await _db.CadAgencies.SingleOrDefaultAsync(x => x.ExternalKey == key);
        if (existing != null) return existing.Id;

        var entity = new CadAgencyEntity
        {
            ExternalKey = key,
            Name = key
        };
        _db.CadAgencies.Add(entity);
        await _db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task<int?> EnsureUnitAsync(string? unitExternalKey)
    {
        if (string.IsNullOrWhiteSpace(unitExternalKey)) return null;
        var key = unitExternalKey.Trim();

        var existing = await _db.Units.SingleOrDefaultAsync(x => x.ExternalKey == key);
        if (existing != null) return existing.Id;

        var entity = new UnitEntity
        {
            ExternalKey = key,
            Name = key
        };
        _db.Units.Add(entity);
        await _db.SaveChangesAsync();
        return entity.Id;
    }

    private static async Task<int?> EnsureValueLookupAsync<TEntity>(
        DbContext db,
        DbSet<TEntity> set,
        string? value,
        Func<string, TEntity> factory)
        where TEntity : class
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();

        // Value lookup pattern: entity has property named "Value".
        var existing = await set.AsQueryable().FirstOrDefaultAsync(e => EF.Property<string>(e, "Value") == v);
        if (existing != null)
            return EF.Property<int>(existing, "Id");

        var entity = factory(v);
        set.Add(entity);
        // caller may call SaveChanges once; but for simplicity here we save immediately
        // because the handler commonly needs the Id.
        await db.SaveChangesAsync();
        return EF.Property<int>(entity, "Id");
    }
}
