
using BTSS.IAR.Kiosk.DispatchEmail.Printing;
using BTSS.IAR.Kiosk.DispatchEmail.Reporting;
using BTSS.IAR.Kiosk.Services;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace BTSS.IAR.Kiosk.Services.DispatchEmail;

public interface IEmailCheckerService
{
    void Start();
    void Stop();
}

/// <summary>
/// Polls Gmail IMAP (using an App Password) for UNREAD messages containing
/// "Fire Station Clear Report", parses them, saves to SQLite, and prints a pivot report.
/// </summary>
public class GmailImapEmailCheckerService : IEmailCheckerService
{
    private readonly IDispatchReportRepository _repo;
    private readonly IFireStationClearReportParser _parser;
    private readonly IPivotReportBuilder _pivot;
    private readonly IPrintService _printer;

    private readonly TimeSpan _pollEvery = TimeSpan.FromSeconds(30);
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public GmailImapEmailCheckerService(
        IDispatchReportRepository repo,
        IFireStationClearReportParser parser,
        IPivotReportBuilder pivot,
        IPrintService printer)
    {
        _repo = repo;
        _parser = parser;
        _pivot = pivot;
        _printer = printer;
    }

    public void Start()
    {
        if (_loop != null && !_loop.IsCompleted) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _loop = null;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        await _repo.InitializeAsync();
        using var timer = new PeriodicTimer(_pollEvery);
        while (!ct.IsCancellationRequested)
        {
            if (AppSettings.PauseChecking)
            {
                try { await timer.WaitForNextTickAsync(ct); }
                catch (OperationCanceledException) { break; }
                continue;
            }
            try
            {
                await CheckOnceAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL CHECK] {ex}");
            }

            try { await timer.WaitForNextTickAsync(ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckOnceAsync(CancellationToken ct)
    {
#if WINDOWS
        var creds = await DispatchEmailCredentialStore.LoadAsync();
        if (creds == null || string.IsNullOrWhiteSpace(creds.EmailAddress) || string.IsNullOrWhiteSpace(creds.AppPassword))
            return;
#else
        // If you later add secure storage for other platforms, implement here.
        return;
#endif
#if WINDOWS
        using var client = new ImapClient();
        await client.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect, ct);
        await client.AuthenticateAsync(creds.EmailAddress, creds.AppPassword, ct);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, ct);

        var uids = await inbox.SearchAsync(SearchQuery.NotSeen, ct);
        foreach (var uid in uids)
        {
            ct.ThrowIfCancellationRequested();

            var message = await inbox.GetMessageAsync(uid, ct);
            var body = GetBestBody(message);

            if (body.IndexOf("Fire Station Clear Report", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var messageId = message.MessageId ?? $"uid:{uid.Id}:{message.Date.UtcDateTime:O}";
            if (await _repo.ExistsByMessageIdAsync(messageId))
            {
                await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
                continue;
            }

            var parsed = _parser.Parse(
                messageId,
                message.Date.ToUniversalTime(),
                message.Subject ?? "",
                body);

            var reportId = await _repo.InsertAsync(parsed);

            var statuses = await _repo.GetStatusesAsync(reportId);
            var pivot = _pivot.Build(statuses);

            var title = $"Fire Station Clear Report  {parsed.DispatchTime:MM-dd-yyyy HH:mm:ss}  Case: {parsed.CaseNumber}";
            var footer = $"Event: {parsed.EventId}  Agency: {parsed.Agency}  Group: {parsed.DispatchGroup}";

            await _printer.PrintAsync(title, pivot, footer);

            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
        }

        await client.DisconnectAsync(true, ct);
# endif
    }

    private static string GetBestBody(MimeMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.TextBody))
            return message.TextBody;

        var html = message.HtmlBody;
        if (string.IsNullOrWhiteSpace(html))
            return "";

        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
    }
}
