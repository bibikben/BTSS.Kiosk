using System.Text.Json;
using BTSS.IAR.Record.Models;
using BTSS.Service.Services;
using Xunit;

namespace BTSS.IntegrationTests;

public class SamplePayloadIntegrationTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Theory]
    [InlineData("BTSS.Service/Samples/IAROpen.json", false)]
    [InlineData("BTSS.Service/Samples/IARClose.json", true)]
    [InlineData("BTSS.Service/Samples/navarino_2025_08_01_134712_9179465.json", true)]
    [InlineData("BTSS.Service/Samples/navarino_2025_08_01_232854_9180278.json", true)]
    [InlineData("BTSS.Service/Samples/otisco_2025_08_03_184535_9182668.json", true)]
    [InlineData("BTSS.Service/Samples/otisco_2025_08_06_171906_9187414.json", true)]
    public void Unified_samples_deserialize_and_render(string relativePath, bool expectedClosed)
    {
        var fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var json = File.ReadAllText(fullPath);
        var model = JsonSerializer.Deserialize<EmergencyCallUnified>(json, EmergencyCallUnifiedJson.Options);

        Assert.NotNull(model);

        var canonicalJson = JsonSerializer.Serialize(model, EmergencyCallUnifiedJson.Options);
        var summary = IncidentSummaryFactory.Create(model!, canonicalJson);

        Assert.False(string.IsNullOrWhiteSpace(summary.IncidentId));
        Assert.Equal(expectedClosed, summary.IsClosed);
        Assert.False(string.IsNullOrWhiteSpace(summary.CallType));
    }

    [Theory]
    [InlineData("BTSS.Service/Samples/DispatchReport1.txt", "F25071900261", "NAF254100108")]
    [InlineData("BTSS.Service/Samples/DispatchReport2.txt", "F17060100135", "NAF174100063")]
    public void Dispatch_reports_parse_expected_headers(string relativePath, string expectedEvent, string expectedCaseNumber)
    {
        var fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var body = File.ReadAllText(fullPath);
        var parser = new FireStationClearReportParser();
        var parsed = parser.Parse("sample", DateTime.UtcNow, Path.GetFileName(fullPath), body);

        Assert.Equal(expectedEvent, parsed.EventId);
        Assert.Equal(expectedCaseNumber, parsed.CaseNumber);
        Assert.NotEmpty(parsed.AssignedUnitStatuses);
    }
}
