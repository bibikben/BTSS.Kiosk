using BTSS.IAR.Record.Models;

namespace BTSS.Service.Models;

public sealed record SftpRemoteFile(
    string RemotePath,
    string FileName,
    long SizeBytes,
    DateTimeOffset? LastWriteUtc);

public sealed record SftpImportCandidate(
    SftpRemoteFile File,
    string RawJson,
    string ContentHash,
    IReadOnlyList<EmergencyCallUnified> Incidents);

public sealed record SftpPostResult(bool Success, int? StatusCode, string? ResponseBody, string? Error);
