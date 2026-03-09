#if WINDOWS
namespace BTSS.IAR.Kiosk.Platforms.Windows.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(string? initialPath = null, CancellationToken ct = default);
}

public sealed class FolderPickerService : IFolderPickerService
{
    public Task<string?> PickFolderAsync(string? initialPath = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
            return Task.FromResult<string?>(initialPath);

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Task.FromResult<string?>(Directory.Exists(documents) ? documents : null);
    }
}
#endif
