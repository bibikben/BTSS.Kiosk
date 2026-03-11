using BTSS.Service.Options;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace BTSS.Service.Services;

public sealed class SftpConnectionFactory(IOptions<ServiceRuntimeOptions> options)
{
    private readonly ServiceRuntimeOptions _options = options.Value;

    public SftpClient CreateClient()
    {
        var settings = _options.SftpImport;
        var connectionInfo = BuildConnectionInfo(settings);
        var client = new SftpClient(connectionInfo);
        client.HostKeyReceived += (_, e) =>
        {
            if (settings.TrustUnknownHostKey)
            {
                e.CanTrust = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.HostKeyFingerprint))
            {
                e.CanTrust = false;
                return;
            }

            var presented = Convert.ToHexString(e.FingerPrint).ToUpperInvariant();
            var expected = NormalizeFingerprint(settings.HostKeyFingerprint);
            e.CanTrust = presented == expected;
        };

        return client;
    }

    private static ConnectionInfo BuildConnectionInfo(SftpImportOptions settings)
    {
        var authMode = (settings.AuthenticationMode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(authMode))
        {
            authMode = "Password";
        }

        AuthenticationMethod[] methods = authMode.ToUpperInvariant() switch
        {
            "PASSWORD" => [ new PasswordAuthenticationMethod(settings.Username, settings.Password ?? string.Empty) ],
            "KEYBOARDINTERACTIVE" => [ CreateKeyboardInteractiveMethod(settings) ],
            "PRIVATEKEY" => [ CreatePrivateKeyMethod(settings) ],
            "PRIVATEKEYWITHPASSPHRASE" => [ CreatePrivateKeyMethod(settings) ],
            _ => throw new SshException($"Unsupported SFTP authentication mode '{settings.AuthenticationMode}'.")
        };

        return new ConnectionInfo(settings.Host, settings.Port, settings.Username, methods);
    }

    private static AuthenticationMethod CreatePrivateKeyMethod(SftpImportOptions settings)
    {
        var keyFile = string.IsNullOrWhiteSpace(settings.PrivateKeyPassphrase)
            ? new PrivateKeyFile(settings.PrivateKeyPath!)
            : new PrivateKeyFile(settings.PrivateKeyPath!, settings.PrivateKeyPassphrase);
        return new PrivateKeyAuthenticationMethod(settings.Username, keyFile);
    }

    private static AuthenticationMethod CreateKeyboardInteractiveMethod(SftpImportOptions settings)
    {
        var method = new KeyboardInteractiveAuthenticationMethod(settings.Username);
        method.AuthenticationPrompt += (_, e) =>
        {
            foreach (var prompt in e.Prompts)
            {
                prompt.Response = settings.Password ?? string.Empty;
            }
        };
        return method;
    }

    private static string NormalizeFingerprint(string value)
        => value.Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();
}
