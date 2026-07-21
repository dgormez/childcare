using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test double for IEmailSender — registered Singleton in OrganisationOnboardingWebAppFactory,
/// overriding Program.cs's real EmailService. Delegates to the real behavior by default (tests
/// that don't care about email still exercise the ordinary dev-mode log-and-return path via an
/// inner EmailService instance), but lets a test opt a specific send into throwing, to prove a
/// send failure doesn't fail the caller's request (feature 005-staff FR-006,
/// /speckit-converge finding F1).
/// </summary>
public class FakeEmailSender(IEmailSender inner) : IEmailSender
{
    public bool ThrowOnStaffInvitation { get; set; }

    /// <summary>Records every SendWaitingListOfferedAsync invocation (feature 012a FR-008/FR-009 tests).</summary>
    public List<(string ToEmail, string ContactName, string ChildName, string LocationName)> WaitingListOfferedCalls { get; } = [];

    public Task SendEmailVerificationAsync(string toEmail, string verifyLink) =>
        inner.SendEmailVerificationAsync(toEmail, verifyLink);

    public Task SendPasswordResetAsync(string toEmail, string resetLink) =>
        inner.SendPasswordResetAsync(toEmail, resetLink);

    public Task SendStaffInvitationAsync(string toEmail, string inviteLink)
    {
        if (ThrowOnStaffInvitation)
            throw new InvalidOperationException("Simulated SMTP failure (test).");

        return inner.SendStaffInvitationAsync(toEmail, inviteLink);
    }

    public Task SendWaitingListOfferedAsync(string toEmail, string contactName, string childName, string locationName)
    {
        WaitingListOfferedCalls.Add((toEmail, contactName, childName, locationName));
        return inner.SendWaitingListOfferedAsync(toEmail, contactName, childName, locationName);
    }

    public bool ThrowOnParentInvitation { get; set; }

    /// <summary>Records every SendParentInvitationAsync invocation (feature 013 tests).</summary>
    public List<(string ToEmail, string InviteLink)> ParentInvitationCalls { get; } = [];

    public Task SendParentInvitationAsync(string toEmail, string inviteLink)
    {
        ParentInvitationCalls.Add((toEmail, inviteLink));
        if (ThrowOnParentInvitation)
            throw new InvalidOperationException("Simulated SMTP failure (test).");

        return inner.SendParentInvitationAsync(toEmail, inviteLink);
    }

    // ── Feature 020 ──────────────────────────────────────────────────────────

    /// <summary>Recipient addresses this test run should simulate a provider failure for
    /// (SendBulkEmailCommandHandler's partial-failure handling, FR-012).</summary>
    public HashSet<string> ThrowOnBulkEmailTo { get; } = [];

    public List<(string ToEmail, string Locale, string Subject, string Body, bool HasAttachment)> BulkEmailCalls { get; } = [];

    public Task SendBulkEmailAsync(
        string toEmail, string locale, string subject, string body,
        (byte[] Bytes, string FileName, string ContentType)? attachment,
        CancellationToken cancellationToken = default)
    {
        BulkEmailCalls.Add((toEmail, locale, subject, body, attachment is not null));
        if (ThrowOnBulkEmailTo.Contains(toEmail))
            throw new InvalidOperationException("Simulated SMTP failure (test).");

        return inner.SendBulkEmailAsync(toEmail, locale, subject, body, attachment, cancellationToken);
    }

    /// <summary>Recipient addresses this test run should simulate a provider failure for
    /// (DailyReportDigestService's/ResendDailyReportEmailCommandHandler's partial-failure
    /// handling, FR-012).</summary>
    public HashSet<string> ThrowOnDailyReportTo { get; } = [];

    public List<(string ToEmail, string Locale, string ChildName, DailySummaryResponse Summary, string? UnsubscribeUrl)> DailyReportCalls { get; } = [];

    public Task SendDailyReportAsync(
        string toEmail, string locale, string childName, DailySummaryResponse summary, string? unsubscribeUrl,
        CancellationToken cancellationToken = default)
    {
        DailyReportCalls.Add((toEmail, locale, childName, summary, unsubscribeUrl));
        if (ThrowOnDailyReportTo.Contains(toEmail))
            throw new InvalidOperationException("Simulated SMTP failure (test).");

        return inner.SendDailyReportAsync(toEmail, locale, childName, summary, unsubscribeUrl, cancellationToken);
    }

    /// <summary>Recipient addresses this test run should simulate a provider failure for
    /// (ClosureNotificationService's partial-failure handling, FR-012).</summary>
    public HashSet<string> ThrowOnClosureNotificationEmailTo { get; } = [];

    public List<(string ToEmail, string Locale, string Title, string Body)> ClosureNotificationEmailCalls { get; } = [];

    public Task SendClosureNotificationEmailAsync(string toEmail, string locale, string title, string body, CancellationToken cancellationToken = default)
    {
        ClosureNotificationEmailCalls.Add((toEmail, locale, title, body));
        if (ThrowOnClosureNotificationEmailTo.Contains(toEmail))
            throw new InvalidOperationException("Simulated SMTP failure (test).");

        return inner.SendClosureNotificationEmailAsync(toEmail, locale, title, body, cancellationToken);
    }

    /// <summary>Recipient addresses this test run should simulate a provider failure for
    /// (SendAnnouncementCommandHandler's partial-failure handling, FR-012).</summary>
    public HashSet<string> ThrowOnAnnouncementEmailTo { get; } = [];

    public List<(string ToEmail, string Locale, string Subject, string Body)> AnnouncementEmailCalls { get; } = [];

    public Task SendAnnouncementEmailAsync(string toEmail, string locale, string subject, string body, CancellationToken cancellationToken = default)
    {
        AnnouncementEmailCalls.Add((toEmail, locale, subject, body));
        if (ThrowOnAnnouncementEmailTo.Contains(toEmail))
            throw new InvalidOperationException("Simulated SMTP failure (test).");

        return inner.SendAnnouncementEmailAsync(toEmail, locale, subject, body, cancellationToken);
    }

    // ── Feature 023 ──────────────────────────────────────────────────────────

    public List<(string ToEmail, string Locale, string ChildName, string LocationName, string ReferenceCode)> EnrollmentConfirmationCalls { get; } = [];

    public Task SendEnrollmentConfirmationAsync(
        string toEmail, string locale, string childName, string locationName, string referenceCode,
        CancellationToken cancellationToken = default)
    {
        EnrollmentConfirmationCalls.Add((toEmail, locale, childName, locationName, referenceCode));
        return inner.SendEnrollmentConfirmationAsync(toEmail, locale, childName, locationName, referenceCode, cancellationToken);
    }

    public List<(string ToEmail, string Locale, string ChildName, string LocationName, DateTime ProposedAt, string AcceptUrl, string DeclineUrl)> TourInvitationCalls { get; } = [];

    public Task SendTourInvitationAsync(
        string toEmail, string locale, string childName, string locationName, DateTime proposedAt,
        string acceptUrl, string declineUrl, CancellationToken cancellationToken = default)
    {
        TourInvitationCalls.Add((toEmail, locale, childName, locationName, proposedAt, acceptUrl, declineUrl));
        return inner.SendTourInvitationAsync(toEmail, locale, childName, locationName, proposedAt, acceptUrl, declineUrl, cancellationToken);
    }

    // ── Feature 024-esignature ───────────────────────────────────────────────

    public List<(string ToEmail, string Locale, string ChildName, string LocationName, string SigningUrl)> ContractSigningInvitationCalls { get; } = [];

    public Task SendContractSigningInvitationAsync(
        string toEmail, string locale, string childName, string locationName, string signingUrl,
        CancellationToken cancellationToken = default)
    {
        ContractSigningInvitationCalls.Add((toEmail, locale, childName, locationName, signingUrl));
        return inner.SendContractSigningInvitationAsync(toEmail, locale, childName, locationName, signingUrl, cancellationToken);
    }

    public List<(string ToEmail, string Locale, string ChildName, byte[] PdfBytes)> SignedContractCalls { get; } = [];

    public Task SendSignedContractAsync(
        string toEmail, string locale, string childName, byte[] pdfBytes,
        CancellationToken cancellationToken = default)
    {
        SignedContractCalls.Add((toEmail, locale, childName, pdfBytes));
        return inner.SendSignedContractAsync(toEmail, locale, childName, pdfBytes, cancellationToken);
    }
}
