using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.Email;

/// <summary>
/// Builds the unsubscribe/re-subscribe links embedded in every daily report email (feature 020,
/// research.md R5). Carries the organisation slug as a query parameter alongside the signed
/// token — mirrors `AuthLinkBuilder.BuildResetUrl`'s exact `?token=...&org={slug}` shape
/// (feature 003) — so the public, unauthenticated endpoint can resolve the correct tenant schema
/// before the token is looked up (constitution Principle I; there is no JWT `tenant_id` claim on
/// this route). Unlike the mobile-deep-link auth flows, these links point at the API's own
/// server-rendered page (`App:ApiBaseUrl`), not a client app scheme.
/// </summary>
internal static class EmailLinkBuilder
{
    public static string BuildUnsubscribeUrl(IConfiguration config, string token, string organisationSlug)
    {
        var baseUrl = config["App:ApiBaseUrl"] ?? throw new InvalidOperationException("App:ApiBaseUrl is not configured.");
        return $"{baseUrl.TrimEnd('/')}/api/email/unsubscribe?token={Uri.EscapeDataString(token)}&org={Uri.EscapeDataString(organisationSlug)}";
    }

    public static string BuildResubscribeUrl(IConfiguration config, string token, string organisationSlug)
    {
        var baseUrl = config["App:ApiBaseUrl"] ?? throw new InvalidOperationException("App:ApiBaseUrl is not configured.");
        return $"{baseUrl.TrimEnd('/')}/api/email/resubscribe?token={Uri.EscapeDataString(token)}&org={Uri.EscapeDataString(organisationSlug)}";
    }
}
