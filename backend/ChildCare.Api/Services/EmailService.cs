using System.Net;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ChildCare.Api.Services;

/// <summary>Implements IEmailSender directly (research.md R6) — no adapter needed, its two
/// public methods already match the port's shape.</summary>
public class EmailService(IConfiguration config, ILogger<EmailService> logger, IEmailTemplateRenderer templateRenderer, IHostEnvironment environment) : IEmailSender
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

    public async Task SendStaffInvitationAsync(string toEmail, string inviteLink)
    {
        if (!TryBuildMessage(toEmail, "You've been invited to join ChildCare", out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Staff invitation link for {Email}: {Link}", toEmail, inviteLink);
            return;
        }

        message!.Body = new TextPart("html")
        {
            Text = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family:sans-serif;max-width:480px;margin:40px auto;color:#111">
                  <h2>You've been invited to join ChildCare</h2>
                  <p>Your director has created a staff account for you. Click below to set your password and log in.</p>
                  <p style="margin:24px 0">
                    <a href="{inviteLink}"
                       style="background:#2563eb;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold">
                      Set your password
                    </a>
                  </p>
                  <p style="color:#666;font-size:14px">
                    This link expires in 7 days. If you weren't expecting this, you can safely ignore this email.
                  </p>
                </body>
                </html>
                """,
        };

        await SendAsync(message);
    }

    public async Task SendWaitingListOfferedAsync(string toEmail, string contactName, string childName, string locationName)
    {
        if (!TryBuildMessage(toEmail, "A place is available at ChildCare", out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Waiting-list offer for {Email} ({Child} at {Location})", toEmail, childName, locationName);
            return;
        }

        message!.Body = new TextPart("html")
        {
            Text = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family:sans-serif;max-width:480px;margin:40px auto;color:#111">
                  <h2>A place is available</h2>
                  <p>Dear {contactName},</p>
                  <p>We're happy to let you know a place is available for {childName} at {locationName}. We'll be in touch shortly to discuss next steps.</p>
                </body>
                </html>
                """,
        };

        await SendAsync(message);
    }

    public async Task SendParentInvitationAsync(string toEmail, string inviteLink)
    {
        if (!TryBuildMessage(toEmail, "You've been invited to join ChildCare", out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Parent invitation link for {Email}: {Link}", toEmail, inviteLink);
            return;
        }

        message!.Body = new TextPart("html")
        {
            Text = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family:sans-serif;max-width:480px;margin:40px auto;color:#111">
                  <h2>You've been invited to join ChildCare</h2>
                  <p>Your child's KDV has invited you to the parent app, where you can see daily updates and message the team directly. Click below to set your password and log in.</p>
                  <p style="margin:24px 0">
                    <a href="{inviteLink}"
                       style="background:#2563eb;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold">
                      Set your password
                    </a>
                  </p>
                  <p style="color:#666;font-size:14px">
                    This link expires in 7 days. If you weren't expecting this, you can safely ignore this email.
                  </p>
                </body>
                </html>
                """,
        };

        await SendAsync(message);
    }

    // ── Feature 020: templated sends ─────────────────────────────────────────

    public async Task SendBulkEmailAsync(
        string toEmail, string locale, string subject, string body,
        (byte[] Bytes, string FileName, string ContentType)? attachment,
        IReadOnlyList<string>? cc = null, IReadOnlyList<string>? bcc = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildMessage(toEmail, subject, out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Bulk email for {Email} not sent.", toEmail);
            return;
        }

        foreach (var address in cc ?? [])
            message!.Cc.Add(MailboxAddress.Parse(address));
        foreach (var address in bcc ?? [])
            message!.Bcc.Add(MailboxAddress.Parse(address));

        var html = await templateRenderer.RenderAsync("bulk-email", locale, new
        {
            SubjectHtml = WebUtility.HtmlEncode(subject),
            BodyHtml = ToHtmlParagraphs(body),
        }, cancellationToken);

        var builder = new BodyBuilder { HtmlBody = html };
        if (attachment is { } file)
            builder.Attachments.Add(file.FileName, file.Bytes, MimeKit.ContentType.Parse(file.ContentType));

        message!.Body = builder.ToMessageBody();
        await SendAsync(message);
    }

    public async Task SendDailyReportAsync(
        string toEmail, string locale, string childName, DailySummaryResponse summary, string? unsubscribeUrl,
        CancellationToken cancellationToken = default)
    {
        var labels = DailyReportEmailLabels.For(locale);
        var subject = string.Format(labels.Subject, childName);

        if (!TryBuildMessage(toEmail, subject, out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Daily report for {Email} not sent.", toEmail);
            return;
        }

        var hasEvents = summary.NapsCount > 0 || summary.BottlesCount > 0 || summary.DiaperChangesCount > 0
            || summary.LatestMood is not null || summary.LatestTemperatureCelsius is not null
            || summary.MedicationAdministered || summary.Activities.Count > 0 || summary.GroupActivities.Count > 0;

        var html = await templateRenderer.RenderAsync("daily-report", locale, new
        {
            Title = string.Format(labels.TitleFormat, WebUtility.HtmlEncode(childName)),
            HasEvents = hasEvents,
            NoUpdatesText = labels.NoUpdatesText,
            NapsLabel = labels.NapsLabel,
            NapsCount = summary.NapsCount,
            BottlesLabel = labels.BottlesLabel,
            BottlesCount = summary.BottlesCount,
            DiapersLabel = labels.DiapersLabel,
            DiapersCount = summary.DiaperChangesCount,
            MoodLabel = labels.MoodLabel,
            MoodText = summary.LatestMood is null ? null : WebUtility.HtmlEncode(summary.LatestMood),
            TemperatureLabel = labels.TemperatureLabel,
            TemperatureText = summary.LatestTemperatureCelsius is null ? null : $"{summary.LatestTemperatureCelsius:0.0}°C",
            MedicationText = summary.MedicationAdministered ? labels.MedicationAdministeredText : null,
            ActivitiesLabel = labels.ActivitiesLabel,
            Activities = summary.Activities.Select(WebUtility.HtmlEncode).ToArray(),
            GroupActivitiesLabel = labels.GroupActivitiesLabel,
            GroupActivities = summary.GroupActivities
                .Select(a => new { Title = WebUtility.HtmlEncode(a.Title), Description = a.Description is null ? null : WebUtility.HtmlEncode(a.Description) })
                .ToArray(),
            UnsubscribeText = unsubscribeUrl is null ? null : labels.UnsubscribeText,
            UnsubscribeUrl = unsubscribeUrl,
        }, cancellationToken);

        message!.Body = new TextPart("html") { Text = html };
        await SendAsync(message);
    }

    public async Task SendClosureNotificationEmailAsync(string toEmail, string locale, string title, string body, CancellationToken cancellationToken = default)
    {
        if (!TryBuildMessage(toEmail, title, out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Closure notification for {Email} not sent.", toEmail);
            return;
        }

        var html = await templateRenderer.RenderAsync("closure-notification", locale, new { Title = WebUtility.HtmlEncode(title), Body = WebUtility.HtmlEncode(body) }, cancellationToken);
        message!.Body = new TextPart("html") { Text = html };
        await SendAsync(message);
    }

    public async Task SendAnnouncementEmailAsync(string toEmail, string locale, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (!TryBuildMessage(toEmail, subject, out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Announcement email for {Email} not sent.", toEmail);
            return;
        }

        var html = await templateRenderer.RenderAsync("announcement", locale, new
        {
            SubjectHtml = WebUtility.HtmlEncode(subject),
            BodyHtml = ToHtmlParagraphs(body),
        }, cancellationToken);
        message!.Body = new TextPart("html") { Text = html };
        await SendAsync(message);
    }

    // ── Feature 023: public online enrollment ────────────────────────────────

    public async Task SendEnrollmentConfirmationAsync(
        string toEmail, string locale, string childName, string locationName, string referenceCode,
        CancellationToken cancellationToken = default)
    {
        var labels = EnrollmentEmailLabels.For(locale);
        if (!TryBuildMessage(toEmail, labels.Subject, out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Enrollment confirmation for {Email} not sent.", toEmail);
            return;
        }

        var html = await templateRenderer.RenderAsync("enrollment-confirmation", locale, new
        {
            Title = string.Format(labels.TitleFormat, WebUtility.HtmlEncode(childName)),
            Body = string.Format(labels.BodyFormat, WebUtility.HtmlEncode(childName), WebUtility.HtmlEncode(locationName)),
            ReferenceLabel = labels.ReferenceLabel,
            ReferenceCode = referenceCode,
        }, cancellationToken);
        message!.Body = new TextPart("html") { Text = html };
        await SendAsync(message);
    }

    public async Task SendTourInvitationAsync(
        string toEmail, string locale, string childName, string locationName, DateTime proposedAt,
        string acceptUrl, string declineUrl, CancellationToken cancellationToken = default)
    {
        var labels = TourInvitationEmailLabels.For(locale);
        if (!TryBuildMessage(toEmail, labels.Subject, out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Tour invitation for {Email} not sent.", toEmail);
            return;
        }

        // Locale-aware day/month names (matches QuestPdfInvoiceGenerator's precedent) — the
        // server's default thread culture must never leak English day/month names into an
        // NL/FR tour-invitation email.
        var culture = System.Globalization.CultureInfo.GetCultureInfo(locale == "en" ? "en-US" : locale);
        var html = await templateRenderer.RenderAsync("tour-invitation", locale, new
        {
            Title = string.Format(labels.TitleFormat, WebUtility.HtmlEncode(childName)),
            Body = string.Format(culture, labels.BodyFormat, WebUtility.HtmlEncode(childName), WebUtility.HtmlEncode(locationName), proposedAt),
            AcceptUrl = acceptUrl,
            DeclineUrl = declineUrl,
            AcceptLabel = labels.AcceptButton,
            DeclineLabel = labels.DeclineButton,
        }, cancellationToken);
        message!.Body = new TextPart("html") { Text = html };
        await SendAsync(message);
    }

    // ── Feature 024-esignature: digital contract e-signature ────────────────

    public async Task SendContractSigningInvitationAsync(
        string toEmail, string locale, string childName, string locationName, string signingUrl,
        CancellationToken cancellationToken = default)
    {
        var labels = ContractSigningEmailLabels.For(locale);
        if (!TryBuildMessage(toEmail, labels.Subject, out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Signing invitation for {Email} not sent.", toEmail);
            return;
        }

        var html = await templateRenderer.RenderAsync("contract-signing-invitation", locale, new
        {
            Title = string.Format(labels.TitleFormat, WebUtility.HtmlEncode(childName)),
            Body = string.Format(labels.BodyFormat, WebUtility.HtmlEncode(childName), WebUtility.HtmlEncode(locationName)),
            SigningUrl = signingUrl,
            SignLabel = labels.SignButton,
        }, cancellationToken);
        message!.Body = new TextPart("html") { Text = html };
        await SendAsync(message);
    }

    public async Task SendSignedContractAsync(
        string toEmail, string locale, string childName, byte[] pdfBytes,
        CancellationToken cancellationToken = default)
    {
        var labels = SignedContractEmailLabels.For(locale);
        if (!TryBuildMessage(toEmail, labels.Subject, out var message))
        {
            logger.LogWarning("Email:SmtpHost not configured. Signed contract copy for {Email} not sent.", toEmail);
            return;
        }

        var html = await templateRenderer.RenderAsync("signed-contract-copy", locale, new
        {
            Title = string.Format(labels.TitleFormat, WebUtility.HtmlEncode(childName)),
            Body = string.Format(labels.BodyFormat, WebUtility.HtmlEncode(childName)),
        }, cancellationToken);

        var builder = new BodyBuilder { HtmlBody = html };
        builder.Attachments.Add("contract.pdf", pdfBytes, MimeKit.ContentType.Parse("application/pdf"));
        message!.Body = builder.ToMessageBody();
        await SendAsync(message);
    }

    /// <summary>HTML-encodes free text, then converts newlines to paragraph breaks — the one
    /// piece of "formatting" a director's plain-text compose box gets (research.md R1: no raw
    /// HTML from the director, to avoid template-injection risk).</summary>
    private static string ToHtmlParagraphs(string plainText)
    {
        var encoded = WebUtility.HtmlEncode(plainText);
        var paragraphs = encoded.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("", paragraphs.Select(p => $"<p style=\"margin:0 0 12px\">{p}</p>"));
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

        // Dev-only: a local Mailpit container's cert is self-signed (docker/mailpit/generate-cert.sh),
        // so the default chain-of-trust validation would otherwise reject it. Never applies outside
        // Development — a real deployment always validates against a trusted CA.
        if (environment.IsDevelopment())
            smtp.ServerCertificateValidationCallback = (_, _, _, _) => true;

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
