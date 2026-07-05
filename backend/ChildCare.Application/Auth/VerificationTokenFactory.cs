using System.Security.Cryptography;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.Auth;

/// <summary>Moved verbatim from the old AuthService — shared between ResendVerificationCommand
/// and (indirectly, via TenantProvisioningService for the very first director) other flows that
/// need a fresh email-verification token.</summary>
internal static class VerificationTokenFactory
{
    public static void SetVerificationToken(TenantUser user)
    {
        user.EmailVerificationToken  = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.EmailVerificationExpiry = DateTime.UtcNow.AddHours(24);
    }
}
