using System.Net;
using System.Net.Mail;

namespace AutoCheck.Services;

/// <summary>One in-memory attachment for an outgoing email.</summary>
public record EmailAttachment(string FileName, byte[] Content, string ContentType);

/// <summary>
/// Sends email via SMTP. Disabled (no-op) while Email:SmtpHost is empty, so the
/// app works out of the box; configure appsettings.json → Email to enable.
/// Registered as a singleton. Returns whether the message was actually sent so
/// callers (e.g. the feedback form) can tell the user the truth instead of a
/// false "надіслано"; failures are logged, never thrown.
/// </summary>
public class EmailService(IConfiguration cfg, ILogger<EmailService> log)
{
    public bool Enabled => !string.IsNullOrEmpty(cfg["Email:SmtpHost"]);

    public async Task<bool> SendAsync(
        string to, string subject, string body,
        IReadOnlyList<EmailAttachment>? attachments = null)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(to) || !to.Contains('@')) return false;

        try
        {
            var host = cfg["Email:SmtpHost"]!;
            var port = int.TryParse(cfg["Email:SmtpPort"], out var p) ? p : 587;
            var user = cfg["Email:SmtpUser"] ?? "";
            var pass = cfg["Email:SmtpPassword"] ?? "";
            var from = cfg["Email:From"];
            var fromName = cfg["Email:FromName"] ?? "AutoCheck";
            if (string.IsNullOrEmpty(from)) from = user;

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = string.IsNullOrEmpty(user) ? null : new NetworkCredential(user, pass),
            };
            using var msg = new MailMessage
            {
                From = new MailAddress(from!, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false,
            };
            msg.To.Add(to);

            foreach (var a in attachments ?? [])
            {
                // Attachment owns the stream and disposes it when msg is disposed.
                var att = new Attachment(new MemoryStream(a.Content), a.FileName, a.ContentType);
                msg.Attachments.Add(att);
            }

            await client.SendMailAsync(msg);
            return true;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Email send failed to {To}: {Subject}", to, subject);
            return false;
        }
    }
}
