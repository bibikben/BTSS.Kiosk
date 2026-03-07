using BTSS.IAR.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BTSS.IAR.Api.Models;

public sealed class LookupUpsertService
{
    private readonly AppDbContext _db;
    public LookupUpsertService(AppDbContext db) => _db = db;

    public async Task<short?> EnsureSourceSystemAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var normalized = code.Trim();
        var existing = await _db.SourceSystems.SingleOrDefaultAsync(x => x.Code == normalized);
        if (existing != null) return existing.Id;

        var entity = new SourceSystemEntity { Code = normalized, Description = $"Dynamic source system: {normalized}" };
        _db.SourceSystems.Add(entity);
        await _db.SaveChangesAsync();
        return entity.Id;
    }
}
