using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BTSS.IAR.Record.Models;
using BTSS.Service.Models;
using BTSS.Service.Options;
using Microsoft.Extensions.Options;

namespace BTSS.Service.Services;

public sealed class IncidentPollingClient(
    HttpClient httpClient,
    OAuthTokenClient tokenClient,
    IOptions<ServiceRuntimeOptions> options,
    ILogger<IncidentPollingClient> logger)
{
    private readonly ServiceRuntimeOptions _options = options.Value;

    public async Task<PollResult> PollAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.IncidentFeedPath);
        var authHeader = await tokenClient.CreateHeaderAsync(cancellationToken);
        if (authHeader is not null)
            request.Headers.Authorization = authHeader;

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var payloadBytes = Encoding.UTF8.GetByteCount(body);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Incident poll failed with status {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
            return new PollResult(Array.Empty<IncidentEnvelope>(), (int)response.StatusCode, payloadBytes, request.RequestUri?.ToString() ?? _options.IncidentFeedPath);
        }

        var incidents = DeserializeIncidents(body);
        return new PollResult(incidents, (int)response.StatusCode, payloadBytes, request.RequestUri?.ToString() ?? _options.IncidentFeedPath);
    }

    private static IReadOnlyList<IncidentEnvelope> DeserializeIncidents(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Array.Empty<IncidentEnvelope>();

        using var document = JsonDocument.Parse(body);
        var candidates = ExtractIncidentArray(document.RootElement);
        var results = new List<IncidentEnvelope>();
        foreach (var candidate in candidates)
        {
            var normalized = candidate.GetRawText();
            var model = JsonSerializer.Deserialize<EmergencyCallUnified>(normalized, EmergencyCallUnifiedJson.Options);
            if (model is null)
                continue;

            var canonicalJson = JsonSerializer.Serialize(model, EmergencyCallUnifiedJson.Options);
            var printable = IncidentSummaryFactory.Create(model, canonicalJson);
            var summary = new PrintTemplateRenderer().Render(printable, PrintTemplateKinds.ClosedSummary);
            results.Add(new IncidentEnvelope(
                IncidentId: printable.IncidentId,
                IsClosed: printable.IsClosed,
                Status: printable.Status,
                UpdatedAtUtc: printable.UpdatedAtUtc,
                Agency: printable.Agency,
                Address: printable.Address,
                CallType: printable.CallType,
                Payload: model,
                CanonicalJson: canonicalJson,
                SummaryText: summary));
        }
        return results;
    }

    private static IEnumerable<JsonElement> ExtractIncidentArray(JsonElement root)
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
}
