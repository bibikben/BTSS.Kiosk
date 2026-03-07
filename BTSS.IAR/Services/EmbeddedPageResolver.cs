namespace BTSS.IAR.Services;

public sealed class EmbeddedPageResolver
{
    public string BuildKorzhQueryUrl(string apiBaseUrl) => Combine(apiBaseUrl, "/korzh/query");
    public string BuildKorzhReportUrl(string apiBaseUrl) => Combine(apiBaseUrl, "/korzh/reports");
    public string BuildKioskDisplayUrl(string startupUrl) => startupUrl;

    private static string Combine(string baseUrl, string relative)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        return new Uri(new Uri(baseUrl), relative).ToString();
    }
}
