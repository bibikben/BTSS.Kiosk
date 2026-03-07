using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using BTSS.IAR.Kiosk.DispatchEmail.Data;
using BTSS.IAR.Kiosk.DispatchEmail.Parsing;
using BTSS.IAR.Kiosk.DispatchEmail.Printing;
using BTSS.IAR.Kiosk.DispatchEmail.Reporting;

namespace BTSS.IAR.Kiosk.DispatchEmail.Email;

public interface IDispatchEmailService
{
    bool IsRunning { get; }
    Task StartAsync(string gmailAddress, string gmailAppPassword, CancellationToken externalCt);
    Task StopAsync();
}

/// <summary>
/// Polls Gmail IMAP for UNSEEN messages. When an unread message contains "Fire Station Clear Report",
/// the body is parsed, persisted to SQLite, pivoted, and printed.
/// </summary>
public class DispatchEmailService : IDispatchEmailService
{
    private readonly GmailImapOptions _opts;
    private readonly IReportRepository _repo;
    private readonly IFireStationClearReportParser _parser;
    private readonly IPivotReportBuilder _pivot;
    private readonly IPrinterService _printer;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public bool IsRunning => _loop != null && !_loop.IsCompleted;

    public DispatchEmailService(
        GmailImapOptions opts,
        IReportRepository repo,
        IFireStationClearReportParser parser,
        IPivotReportBuilder pivot,
        IPrinterService printer)
    {
        _opts = opts;
        _repo = repo;
        _parser = parser;
        _pivot = pivot;
        _printer = printer;
    }

    public async Task StartAsync(string gmailAddress, string gmailAppPassword, CancellationToken externalCt)
    {
        if (IsRunning) return;

        await _repo.InitializeAsync();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        _loop = Task.Run(() => LoopAsync(gmailAddress, gmailAppPassword, _cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts == null) return;
        _cts.Cancel();

        if (_loop != null)
        {
            try { await _loop; }
            catch { /* swallow */ }
        }

        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    private async Task LoopAsync(string gmailAddress, string gmailAppPassword, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CheckOnceAsync(gmailAddress, gmailAppPassword, ct);
            }
            catch
            {
                // TODO: add logging hook if desired
            }

            await Task.Delay(_opts.PollInterval, ct);
        }
    }

    private async Task CheckOnceAsync(string gmailAddress, string gmailAppPassword, CancellationToken ct)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(_opts.Host, _opts.Port, SecureSocketOptions.SslOnConnect, ct);
        await client.AuthenticateAsync(gmailAddress, gmailAppPassword, ct);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, ct);

        var uids = await inbox.SearchAsync(SearchQuery.NotSeen, ct);
        if (uids.Count == 0)
        {
            await client.DisconnectAsync(true, ct);
            return;
        }

        foreach (var uid in uids)
        {
            ct.ThrowIfCancellationRequested();

            var message = await inbox.GetMessageAsync(uid, ct);
            var body = GetBestBody(message);

            if (body.IndexOf("Fire Station Clear Report", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var messageId = message.MessageId ?? $"{uid.Id}:{message.Date.UtcDateTime:O}";

            if (await _repo.ExistsByMessageIdAsync(messageId))
            {
                await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
                continue;
            }

            var parsed = _parser.Parse(
                messageId,
                message.Date,
                message.Subject ?? "",
                body);

            var reportId = await _repo.InsertReportAsync(parsed);
            var statuses = await _repo.GetStatusesAsync(reportId);
            var pivot = _pivot.Build(statuses);

            var title = $"Fire Station Clear Report - {parsed.ReceivedUtc.ToLocalTime():G}";
            var footer = $"MessageId: {parsed.MessageId}";
            await _printer.PrintAsync(title, pivot, footer);

            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
        }

        await client.DisconnectAsync(true, ct);
    }

    private static string GetBestBody(MimeMessage message)
    {
        var text = message.TextBody;
        if (!string.IsNullOrWhiteSpace(text))
            return text;

        var html = message.HtmlBody ?? string.Empty;
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
    }
}
