using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BTSS.IAR.Record.Models;
using BTSS.Service.Models;
using BTSS.Service.Options;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace BTSS.Service.Services;

public sealed class SftpIncidentFileScanner(
    SftpConnectionFactory connectionFactory,
    IOptions<ServiceRuntimeOptions> options,
    ILogger<SftpIncidentFileScanner> logger)
{
    private readonly ServiceRuntimeOptions _options = options.Value;

    public async Task<IReadOnlyList<SftpRemoteFile>> ListFilesAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            using var client = connectionFactory.CreateClient();
            client.Connect();
            try
            {
                var results = new List<SftpRemoteFile>();
                WalkDirectory(client, _options.SftpImport.RemoteDirectory, results);
                return (IReadOnlyList<SftpRemoteFile>)results
                    .OrderBy(x => x.RemotePath, StringComparer.OrdinalIgnoreCase)
                    .Take(_options.SftpImport.MaxFilesPerCycle)
                    .ToArray();
            }
            finally
            {
                if (client.IsConnected)
                    client.Disconnect();
            }
        }, cancellationToken);
    }

    public async Task<SftpImportCandidate> ReadCandidateAsync(SftpRemoteFile file, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            using var client = connectionFactory.CreateClient();
            client.Connect();
            try
            {
                using var memory = new MemoryStream();
                client.DownloadFile(file.RemotePath, memory);
                var rawJson = Encoding.UTF8.GetString(memory.ToArray());
                var incidents = ParseIncidents(rawJson, _options.SftpImport.MaxIncidentsPerFile);
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawJson)));
                return new SftpImportCandidate(file, rawJson, hash, incidents);
            }
            finally
            {
                if (client.IsConnected)
                    client.Disconnect();
            }
        }, cancellationToken);
    }

    private void WalkDirectory(SftpClient client, string path, List<SftpRemoteFile> results)
    {
        foreach (var entry in client.ListDirectory(path))
        {
            if (entry.Name is "." or "..")
                continue;

            if (entry.IsDirectory)
            {
                if (_options.SftpImport.Recursive)
                    WalkDirectory(client, entry.FullName, results);
                continue;
            }

            if (!IsMatch(entry.Name, _options.SftpImport.FileSearchPattern))
                continue;

            results.Add(new SftpRemoteFile(
                entry.FullName,
                entry.Name,
                entry.Attributes.Size,
                entry.Attributes.LastWriteTimeUtc));
        }
    }

    private static IReadOnlyList<EmergencyCallUnified> ParseIncidents(string rawJson, int maxIncidentsPerFile)
    {
        using var document = JsonDocument.Parse(rawJson);
        var results = new List<EmergencyCallUnified>();

        foreach (var element in ExtractCandidates(document.RootElement))
        {
            var incident = EmergencyCallUnifiedJson.Deserialize(element.GetRawText());
            if (incident is null)
                continue;

            if (string.IsNullOrWhiteSpace(incident.GetCallIdentifier()))
                continue;

            results.Add(incident);
            if (results.Count >= maxIncidentsPerFile)
                break;
        }

        return results;
    }

    private static IEnumerable<JsonElement> ExtractCandidates(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().ToArray();

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                return items.EnumerateArray().ToArray();

            if (root.TryGetProperty("incidents", out var incidents) && incidents.ValueKind == JsonValueKind.Array)
                return incidents.EnumerateArray().ToArray();

            return new[] { root };
        }

        return Array.Empty<JsonElement>();
    }

    private static bool IsMatch(string fileName, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*")
            return true;

        if (pattern == "*.json")
            return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        if (!pattern.Contains('*'))
            return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);

        var parts = pattern.Split('*', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var startIndex = 0;
        foreach (var part in parts)
        {
            var idx = fileName.IndexOf(part, startIndex, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return false;
            startIndex = idx + part.Length;
        }

        return true;
    }
}
