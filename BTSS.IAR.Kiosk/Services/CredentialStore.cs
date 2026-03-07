#if WINDOWS
using System.Text;
using System.Text.Json;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage.Streams;

namespace BTSS.IAR.Kiosk.Services;

public static class CredentialStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BTSS.IAR.Kiosk");

    private static readonly string FilePath = Path.Combine(Dir, "creds.bin");

    public static async Task SaveAsync(StoredCreds creds)
    {
        Directory.CreateDirectory(Dir);

        var json = JsonSerializer.Serialize(creds);
        var plain = Encoding.UTF8.GetBytes(json);

        // Encrypt for current user
        var provider = new DataProtectionProvider("LOCAL=user");
        IBuffer plainBuf = CryptographicBuffer.CreateFromByteArray(plain);
        IBuffer protectedBuf = await provider.ProtectAsync(plainBuf);

        CryptographicBuffer.CopyToByteArray(protectedBuf, out byte[] cipher);
        File.WriteAllBytes(FilePath, cipher);
    }

    public static async Task<StoredCreds?> LoadAsync()
    {
        if (!File.Exists(FilePath)) return null;

        var cipher = File.ReadAllBytes(FilePath);

        var provider = new DataProtectionProvider();
        IBuffer cipherBuf = CryptographicBuffer.CreateFromByteArray(cipher);
        IBuffer plainBuf = await provider.UnprotectAsync(cipherBuf);

        CryptographicBuffer.CopyToByteArray(plainBuf, out byte[] plain);
        var json = Encoding.UTF8.GetString(plain);

        return JsonSerializer.Deserialize<StoredCreds>(json);
    }

    public static void Clear()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }
}
#endif