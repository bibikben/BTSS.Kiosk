namespace BTSS.IAR.Kiosk.DispatchEmail.Email;

public class GmailImapOptions
{
    public string Host { get; set; } = "imap.gmail.com";
    public int Port { get; set; } = 993;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);
}
