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
    /// Feature 005-staff. Body content is intentionally English-only raw HTML, matching the
    /// existing pattern above (spec.md FR-014 explicitly scopes email body content out of this
    /// feature's i18n requirement — feature 019 owns the templating/i18n rework).
    /// </summary>
    Task SendStaffInvitationAsync(string toEmail, string inviteLink);

    /// <summary>
    /// Feature 012a-waiting-list, FR-008. Body content is intentionally English-only raw HTML,
    /// following the same precedent as SendStaffInvitationAsync above — feature 019 owns the
    /// email-templating/i18n rework, not this feature.
    /// </summary>
    Task SendWaitingListOfferedAsync(string toEmail, string contactName, string childName, string locationName);

    /// <summary>
    /// Feature 013-parent-communication. Body content is intentionally English-only raw HTML,
    /// following the same precedent as SendStaffInvitationAsync above.
    /// </summary>
    Task SendParentInvitationAsync(string toEmail, string inviteLink);
}
