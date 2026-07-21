using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.Contracts;

/// <summary>
/// Builds the signing link embedded in the signing-invitation email (feature 024-esignature,
/// research.md R1). Points at the web app's public `/sign` route (not a server-rendered API
/// page — the signing flow needs real client-side interaction: scroll-gating, canvas signature
/// capture, IBAN validation), carrying the signed token plus the organisation slug so the
/// tenant-exempt public endpoints can resolve the correct tenant schema before the token is
/// looked up (constitution Principle I; there is no JWT `tenant_id` claim on this route) —
/// mirrors `EnrollmentLinkBuilder.BuildPublicEnrollmentUrl`'s `App:XxxBaseUrl` convention.
/// </summary>
internal static class ContractSigningLinkBuilder
{
    public static string BuildSigningUrl(IConfiguration config, string token, string organisationSlug)
    {
        var baseUrl = config["App:ContractSigningBaseUrl"] ?? "http://localhost:3000/sign";
        return $"{baseUrl.TrimEnd('/')}?token={Uri.EscapeDataString(token)}&org={Uri.EscapeDataString(organisationSlug)}";
    }
}
