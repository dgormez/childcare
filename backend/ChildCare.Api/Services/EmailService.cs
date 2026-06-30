using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ChildCare.Api.Services;

public class EmailService(IConfiguration config, ILogger<EmailService> logger)
{
    public async Task SendEmailVerificationAsync(string toEmail, string verifyLink)
    {
        if (!TryBuildMessage(toEmail, "Verify your ChildCare email", out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Verify link for {Email}: {Link}", toEmail, verifyLink);
            return;
        }

        message!.Body = new TextPart("html")
        {
            Text = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family:sans-serif;max-width:480px;margin:40px auto;color:#111">
                  <h2>Verify your email</h2>
                  <p>Thanks for signing up! Click below to verify your email address.</p>
                  <p style="margin:24px 0">
                    <a href="{verifyLink}"
                       style="background:#2563eb;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold">
                      Verify email
                    </a>
                  </p>
                  <p style="color:#666;font-size:14px">
                    This link expires in 24 hours. If you didn't create an account, you can safely ignore this email.
                  </p>
                </body>
                </html>
                """,
        };

        await SendAsync(message);
    }

    public async Task SendPasswordResetAsync(string toEmail, string resetLink)
    {
        if (!TryBuildMessage(toEmail, "Reset your ChildCare password", out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Password reset link for {Email}: {Link}", toEmail, resetLink);
            return;
        }

        message!.Body = new TextPart("html")
        {
            Text = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family:sans-serif;max-width:480px;margin:40px auto;color:#111">
                  <h2>Reset your password</h2>
                  <p>We received a request to reset the password for your ChildCare account.</p>
                  <p style="margin:24px 0">
                    <a href="{resetLink}"
                       style="background:#2563eb;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold">
                      Reset password
                    </a>
                  </p>
                  <p style="color:#666;font-size:14px">
                    This link expires in 1 hour. If you didn't request a reset, you can safely ignore this email.
                  </p>
                </body>
                </html>
                """,
        };

        await SendAsync(message);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns false (and null message) when SMTP is not configured — caller should log and return early.</summary>
    private bool TryBuildMessage(string toEmail, string subject, out MimeMessage? message)
    {
        if (string.IsNullOrEmpty(config["Email:SmtpHost"]))
        {
            message = null;
            return false;
        }

        var from = config["Email:FromAddress"] ?? "noreply@childcare.app";
        message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        return true;
    }

    private async Task SendAsync(MimeMessage message)
    {
        using var smtp = new SmtpClient();

        var host         = config["Email:SmtpHost"]!;
        var port         = int.Parse(config["Email:SmtpPort"] ?? "587");
        var socketOption = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await smtp.ConnectAsync(host, port, socketOption);

        var username = config["Email:Username"];
        var password = config["Email:Password"] ?? string.Empty;
        if (!string.IsNullOrEmpty(username))
            await smtp.AuthenticateAsync(username, password);

        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(quit: true);
    }
}
