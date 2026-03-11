using BTSS.Service.Data;
using BTSS.Service.Entities;
using BTSS.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace BTSS.Service.Services;

public sealed class SftpImportLedger(ServiceDbContext db)
{
    public async Task<SftpImportFileEntity?> FindByRemotePathAsync(string remotePath, CancellationToken cancellationToken)
        => await db.SftpImportFiles.FirstOrDefaultAsync(x => x.RemotePath == remotePath, cancellationToken);

    public async Task<bool> ShouldProcessAsync(SftpRemoteFile file, string contentHash, bool reprocessWhenChanged, CancellationToken cancellationToken)
    {
        var existing = await FindByRemotePathAsync(file.RemotePath, cancellationToken);
        if (existing is null)
            return true;

        if (!string.Equals(existing.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
            return reprocessWhenChanged;

        return existing.Status is not "processed";
    }

    public async Task<SftpImportFileEntity> RecordSeenAsync(SftpRemoteFile file, string contentHash, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await FindByRemotePathAsync(file.RemotePath, cancellationToken);
        if (existing is null)
        {
            existing = new SftpImportFileEntity
            {
                RemotePath = file.RemotePath,
                FileName = file.FileName,
                FirstSeenUtc = now,
                Status = "new"
            };
            db.SftpImportFiles.Add(existing);
        }

        var previousHash = existing.ContentHash;
        existing.FileName = file.FileName;
        existing.SizeBytes = file.SizeBytes;
        existing.RemoteLastWriteUtc = file.LastWriteUtc;
        existing.ContentHash = contentHash;
        existing.LastSeenUtc = now;

        if (existing.LastProcessedUtc.HasValue && existing.Status == "processed" && !string.Equals(previousHash, contentHash, StringComparison.OrdinalIgnoreCase))
            existing.Status = "changed";

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task MarkProcessedAsync(long fileId, IReadOnlyList<SftpPostResult> posts, IReadOnlyList<string> incidentIds, CancellationToken cancellationToken)
    {
        var file = await db.SftpImportFiles.FirstAsync(x => x.Id == fileId, cancellationToken);
        file.Status = posts.Any(x => !x.Success) ? "failed" : "processed";
        file.LastProcessedUtc = DateTimeOffset.UtcNow;
        file.AttemptCount += 1;
        file.Error = posts.FirstOrDefault(x => !x.Success)?.Error;

        db.SftpImportIncidents.RemoveRange(db.SftpImportIncidents.Where(x => x.SftpImportFileId == fileId));
        foreach (var pair in incidentIds.Select((id, idx) => new { id, idx }))
        {
            var post = pair.idx < posts.Count ? posts[pair.idx] : new SftpPostResult(false, null, null, "No post result recorded.");
            db.SftpImportIncidents.Add(new SftpImportIncidentEntity
            {
                SftpImportFileId = fileId,
                IncidentId = pair.id,
                ImportedAtUtc = DateTimeOffset.UtcNow,
                ApiPosted = post.Success,
                ApiStatusCode = post.StatusCode,
                Error = post.Error
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(long fileId, string error, CancellationToken cancellationToken)
    {
        var file = await db.SftpImportFiles.FirstAsync(x => x.Id == fileId, cancellationToken);
        file.Status = "failed";
        file.LastProcessedUtc = DateTimeOffset.UtcNow;
        file.AttemptCount += 1;
        file.Error = error;
        await db.SaveChangesAsync(cancellationToken);
    }
}
