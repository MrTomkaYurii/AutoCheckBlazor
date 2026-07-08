using System.Net;
using System.Net.Mail;

namespace AutoCheck.Services;

/// <summary>
/// Sends email via SMTP. Disabled (no-op) while Email:SmtpHost is empty, so the
/// app works out of the box; configure appsettings.json → Email to enable.
/// Registered as a singleton; failures are logged and never thrown to callers.
/// </summary>
public class EmailService(IConfiguration cfg, ILogger<EmailService> log)
{
    public bool Enabled => !string.IsNullOrEmpty(cfg["Email:SmtpHost"]);

    public async Task SendAsync(string to, string subject, string body)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(to) || !to.Contains('@')) return;

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
            await client.SendMailAsync(msg);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Email send failed to {To}: {Subject}", to, subject);
        }
    }
}
