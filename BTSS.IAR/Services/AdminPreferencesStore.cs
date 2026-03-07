using System.Text.Json;
using BTSS.IAR.Models;

namespace BTSS.IAR.Services;

public sealed class AdminPreferencesStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BTSS.IAR", "admin-preferences.json");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<AdminPreferences> LoadAsync()
    {
        try
        {
            if (!File.Exists(_path)) return new AdminPreferences();
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<AdminPreferences>(stream, JsonOptions) ?? new AdminPreferences();
        }
        catch
        {
            return new AdminPreferences();
        }
    }

    public async Task SaveAsync(AdminPreferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, preferences, JsonOptions);
    }
}
