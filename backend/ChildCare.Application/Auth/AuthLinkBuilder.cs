using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.Auth;

/// <summary>
/// Builds the deep links emailed for verification/reset (moved from the old AuthService),
/// now carrying the organisation slug as a query parameter (research.md R2) so the exempt
/// VerifyEmail/ResetPassword commands can resolve the correct tenant schema when the link is
/// later clicked, without a public token→tenant index.
/// </summary>
internal static class AuthLinkBuilder
{
    public static string BuildVerifyUrl(IConfiguration config, string token, string organisationSlug)
    {
        var verifyBase = config["App:VerifyBaseUrl"]
                       ?? $"{config["App:Scheme"] ?? "childcare"}://verify-email";
        return $"{verifyBase}?token={token}&org={organisationSlug}";
    }

    public static string BuildResetUrl(IConfiguration config, string token, string organisationSlug)
    {
        var resetBase = config["App:ResetBaseUrl"]
                      ?? $"{config["App:Scheme"] ?? "childcare"}://reset-password";
        return $"{resetBase}?token={token}&org={organisationSlug}";
    }
}
