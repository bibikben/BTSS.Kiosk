namespace BTSS.Service.Entities;

public sealed class SftpImportFileEntity
{
    public long Id { get; set; }
    public string RemotePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset? RemoteLastWriteUtc { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public DateTimeOffset? LastProcessedUtc { get; set; }
    public string Status { get; set; } = "new";
    public int AttemptCount { get; set; }
    public string? Error { get; set; }
}

public sealed class SftpImportIncidentEntity
{
    public long Id { get; set; }
    public long SftpImportFileId { get; set; }
    public string IncidentId { get; set; } = string.Empty;
    public DateTimeOffset ImportedAtUtc { get; set; }
    public bool ApiPosted { get; set; }
    public int? ApiStatusCode { get; set; }
    public string? Error { get; set; }
}
