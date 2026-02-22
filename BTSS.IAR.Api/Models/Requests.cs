namespace BTSS.IAR.Api.Models;

public sealed class ReceiveCallDetailsRequest
{
    public string SystemIdentifier { get; set; } = "IAR"; // IAR | EmailText
    public string Payload { get; set; } = "";              // json or raw text
    public int AgencyIdentifier { get; set; }
}

public sealed class ReceiveCallDetailsResponse
{
    public string CallIdentifier { get; set; } = "";
    public double? EstimatedMilesFromStation { get; set; }
}

public sealed class CheckForCloseRequest
{
    public int AgencyIdentifier { get; set; }
}

public sealed class TokenRequest
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string GrantType { get; set; } = "client_credentials";
}

public sealed class SeedClientRequest
{
    public int AgencyId { get; set; }
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string? AgencyName { get; set; }
}

public sealed class AgencyUpsertRequest
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public double? StationLatitude { get; set; }
    public double? StationLongitude { get; set; }
}
