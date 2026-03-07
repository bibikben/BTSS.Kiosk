using BTSS.Service.Models;
using BTSS.Service.Options;
using Microsoft.Extensions.Options;

namespace BTSS.Service.Services;

public sealed class PrintJobWriter(IOptions<ServiceRuntimeOptions> options, PrintTemplateRenderer renderer)
{
    private readonly ServiceRuntimeOptions _options = options.Value;

    public async Task<(string OutputPath, string Body)> WriteAsync(PrintableIncidentSummary summary, string templateKind, CancellationToken cancellationToken)
    {
        var root = _options.ResolvePrintOutputDirectory();
        Directory.CreateDirectory(root);
        var safeId = string.Concat(summary.IncidentId.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var suffix = templateKind == PrintTemplateKinds.DetailReprint ? "detail" : "summary";
        var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{safeId}_{suffix}.txt";
        var fullPath = Path.Combine(root, fileName);
        var body = renderer.Render(summary, templateKind);
        await File.WriteAllTextAsync(fullPath, body, cancellationToken);
        return (fullPath, body);
    }
}
