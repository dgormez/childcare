namespace ChildCare.Application.Common;

/// <summary>
/// Port over EmailService (research.md R6), letting Application-layer auth commands send
/// verification/reset emails without depending on ChildCare.Api's concrete MailKit-based type.
/// </summary>
public interface IEmailSender
{
    Task SendEmailVerificationAsync(string toEmail, string verifyLink);

    Task SendPasswordResetAsync(string toEmail, string resetLink);
}
