using System.Net.Http.Headers;
using System.Text;
using BTSS.IAR.Record.Models;
using BTSS.Service.Models;
using BTSS.Service.Options;
using Microsoft.Extensions.Options;

namespace BTSS.Service.Services;

public sealed class SftpIncidentIngestClient(
    HttpClient httpClient,
    OAuthTokenClient tokenClient,
    IOptions<ServiceRuntimeOptions> options)
{
    private readonly ServiceRuntimeOptions _options = options.Value;

    public async Task<SftpPostResult> PostAsync(EmergencyCallUnified incident, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.SftpIngestPath);
            var auth = await tokenClient.CreateHeaderAsync(cancellationToken, _options.SftpIngestScope);
            if (auth is not null)
                request.Headers.Authorization = auth;

            var json = EmergencyCallUnifiedJson.Serialize(incident);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new SftpPostResult(response.IsSuccessStatusCode, (int)response.StatusCode, body, response.IsSuccessStatusCode ? null : body);
        }
        catch (Exception ex)
        {
            return new SftpPostResult(false, null, null, ex.Message);
        }
    }
}
