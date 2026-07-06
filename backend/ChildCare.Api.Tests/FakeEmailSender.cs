using ChildCare.Application.Common;

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
}
