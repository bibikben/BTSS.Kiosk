using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;

namespace BTSS.IAR.Api.Data;

public class ApiClient
{
    public int Id { get; set; }
    public int AgencyId { get; set; }

    public string ClientId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsEnabled { get; set; } = true;

    public short? SourceSystemId { get; set; }
    public SourceSystemEntity? SourceSystem { get; set; }

    public string GlobalSettingsJson { get; set; } = "{}";
    public string DeviceSettingsJson { get; set; } = "[]";
    public string DisplayRegistrationsJson { get; set; } = "[]";
    public string DeviceCommandsJson { get; set; } = "[]";
    public string AllowedScopesJson { get; set; } = "[]";

    public string ClientSecretHash { get; set; } = "";
    public string ClientSecretSalt { get; set; } = "";
    public int ClientSecretVersion { get; set; } = 1;
    public DateTime? ClientSecretRotatedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string EffectiveSourceSystemCode => SourceSystem?.Code ?? "IAR";

    [NotMapped]
    public IReadOnlyList<string> AllowedScopes
    {
        get
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(AllowedScopesJson) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }

    public void SetClientSecret(string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("Client secret is required.", nameof(clientSecret));

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(clientSecret, salt, 100_000, HashAlgorithmName.SHA256, 32);

        ClientSecretSalt = Convert.ToBase64String(salt);
        ClientSecretHash = Convert.ToBase64String(hash);
        ClientSecretRotatedAtUtc = DateTime.UtcNow;
        ClientSecretVersion = ClientSecretVersion <= 0 ? 1 : ClientSecretVersion + 1;
    }

    public bool VerifyClientSecret(string? clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(ClientSecretSalt) || string.IsNullOrWhiteSpace(ClientSecretHash))
            return false;

        try
        {
            var salt = Convert.FromBase64String(ClientSecretSalt);
            var expectedHash = Convert.FromBase64String(ClientSecretHash);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(clientSecret, salt, 100_000, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}

public enum SourceSystemCode : short
{
    IAR = 1,
    EmailText = 2
}

public class SourceSystemEntity
{
    public short Id { get; set; }
    public string Code { get; set; } = "";
    public string? Description { get; set; }
}
