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
}
