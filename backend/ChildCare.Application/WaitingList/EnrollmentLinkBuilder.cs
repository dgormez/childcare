using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.WaitingList;

/// <summary>
/// Builds the two public-facing URLs feature 023 introduces (research.md R1/R4).
/// BuildPublicEnrollmentUrl points at the web app's new public route — a shareable link, not a
/// token-bearing one, keyed by org/location slug alone, mirroring AuthLinkBuilder's
/// `App:XxxBaseUrl` convention. BuildTourResponseUrl points at the API's own server-rendered
/// page, exactly like EmailLinkBuilder's unsubscribe/resubscribe links — a signed token plus the
/// `org` slug so the exempt endpoint can resolve the correct tenant schema before the token is
/// looked up (there is no JWT `tenant_id` claim on this route).
/// </summary>
internal static class EnrollmentLinkBuilder
{
    public static string BuildPublicEnrollmentUrl(IConfiguration config, string organisationSlug, string locationSlug)
    {
        var baseUrl = config["App:PublicEnrollmentBaseUrl"] ?? "http://localhost:3000/enroll";
        return $"{baseUrl.TrimEnd('/')}/{Uri.EscapeDataString(organisationSlug)}/{Uri.EscapeDataString(locationSlug)}";
    }

    public static string BuildTourResponseUrl(IConfiguration config, string token, string organisationSlug, string response)
    {
        var baseUrl = config["App:ApiBaseUrl"] ?? throw new InvalidOperationException("App:ApiBaseUrl is not configured.");
        return $"{baseUrl.TrimEnd('/')}/api/public/enrollment/tour-response" +
               $"?token={Uri.EscapeDataString(token)}&org={Uri.EscapeDataString(organisationSlug)}&response={Uri.EscapeDataString(response)}";
    }
}
