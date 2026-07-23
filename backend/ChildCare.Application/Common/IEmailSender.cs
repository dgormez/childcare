using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Common;

/// <summary>
/// Port over EmailService (research.md R6), letting Application-layer auth commands send
/// verification/reset emails without depending on ChildCare.Api's concrete MailKit-based type.
/// </summary>
public interface IEmailSender
{
    Task SendEmailVerificationAsync(string toEmail, string verifyLink);

    Task SendPasswordResetAsync(string toEmail, string resetLink);

    /// <summary>
    /// Feature 005-staff. Body content is intentionally English-only raw HTML — an accepted,
    /// known gap left out of scope by feature 020 (spec.md Assumptions), which owns the
    /// templating/i18n rework only for the new send paths below (bulk email, daily report,
    /// closure/announcement email). Transactional auth emails (this method and the two below)
    /// are unchanged; an earlier doc comment here incorrectly attributed this rework to feature
    /// 019 (IKT subsidy integration, unrelated to email) — corrected during feature 020.
    /// </summary>
    Task SendStaffInvitationAsync(string toEmail, string inviteLink);

    /// <summary>Feature 012a-waiting-list, FR-008. Same accepted English-only gap as SendStaffInvitationAsync above.</summary>
    Task SendWaitingListOfferedAsync(string toEmail, string contactName, string childName, string locationName);

    /// <summary>Feature 013-parent-communication. Same accepted English-only gap as SendStaffInvitationAsync above.</summary>
    Task SendParentInvitationAsync(string toEmail, string inviteLink);

    /// <summary>
    /// Feature 020, FR-001/FR-002/FR-003. <paramref name="subject"/>/<paramref name="body"/> are
    /// the director's own free text, rendered via the shared Scriban layout in
    /// <paramref name="locale"/> — the implementation HTML-encodes them before interpolation
    /// (research.md R1's "no raw HTML from the director" rule). <paramref name="attachment"/>
    /// bytes are fetched once by the caller (SendBulkEmailCommandHandler, which also verifies
    /// the size cap there) and reused across every recipient in the batch, rather than each call
    /// re-downloading the same object from GCS.
    /// </summary>
    Task SendBulkEmailAsync(
        string toEmail, string locale, string subject, string body,
        (byte[] Bytes, string FileName, string ContentType)? attachment,
        IReadOnlyList<string>? cc = null, IReadOnlyList<string>? bcc = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Feature 020, FR-004/FR-005/FR-006. Renders <paramref name="summary"/> (feature 013's
    /// existing daily-summary read-model, already consent-filtered — research.md R7) into the
    /// daily-report template in <paramref name="locale"/>, with an unsubscribe footer link.
    /// <paramref name="unsubscribeUrl"/> is null for an on-demand resend (FR-009 — no
    /// unsubscribe affordance on a resend the recipient didn't opt into receiving automatically).
    /// </summary>
    Task SendDailyReportAsync(
        string toEmail, string locale, string childName, DailySummaryResponse summary, string? unsubscribeUrl,
        CancellationToken cancellationToken = default);

    /// <summary>Feature 020, FR-010 — closure/cancellation email alongside the existing push/in-app channel.</summary>
    Task SendClosureNotificationEmailAsync(string toEmail, string locale, string title, string body, CancellationToken cancellationToken = default);

    /// <summary>Feature 020, FR-011 — announcement email alongside the existing push/in-app channel.</summary>
    Task SendAnnouncementEmailAsync(string toEmail, string locale, string subject, string body, CancellationToken cancellationToken = default);

    /// <summary>Feature 023, FR-009 — sent immediately after a public enrollment submission, in
    /// the language the parent selected on the form.</summary>
    Task SendEnrollmentConfirmationAsync(
        string toEmail, string locale, string childName, string locationName, string referenceCode,
        CancellationToken cancellationToken = default);

    /// <summary>Feature 023, FR-015 — a director-initiated tour invitation with accept/decline
    /// links; re-sending overwrites the previous invitation rather than accumulating a history
    /// (research.md R2).</summary>
    Task SendTourInvitationAsync(
        string toEmail, string locale, string childName, string locationName, DateTime proposedAt,
        string acceptUrl, string declineUrl, CancellationToken cancellationToken = default);

    /// <summary>Feature 024-esignature, FR-003 — the secure signing link, in the resolved
    /// primary contact's locale.</summary>
    Task SendContractSigningInvitationAsync(
        string toEmail, string locale, string childName, string locationName, string signingUrl,
        CancellationToken cancellationToken = default);

    /// <summary>Feature 024-esignature, FR-011 — sent identically to the parent and to every
    /// director, with the final signed PDF attached.</summary>
    Task SendSignedContractAsync(
        string toEmail, string locale, string childName, byte[] pdfBytes,
        CancellationToken cancellationToken = default);
}
