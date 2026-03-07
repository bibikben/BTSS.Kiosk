namespace BTSS.IAR.Api.Models.Dtos;

/// <summary>
/// Request wrapper for email-body ingestion.
/// </summary>
public sealed class ReceiveEmailTextCallDetailsRequest
{
    public int AgencyIdentifier { get; set; }

    /// <summary>
    /// The raw email body text (for example a "Fire Station Clear Report").
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Optional email subject.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Optional from/sender string.
    /// </summary>
    public string? From { get; set; }
}
