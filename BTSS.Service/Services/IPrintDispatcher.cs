using BTSS.Service.Entities;

namespace BTSS.Service.Services;

public interface IPrintDispatcher
{
    Task<IReadOnlyList<string>> GetAvailablePrintersAsync(CancellationToken cancellationToken);
    Task<(string Status, string? Error)> DispatchAsync(PrintLedgerEntity entry, CancellationToken cancellationToken);
}
