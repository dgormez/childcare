using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.Staff;

/// <summary>
/// Builds the deep link emailed for a staff invitation, mirroring AuthLinkBuilder (feature 003)
/// — carries the organisation slug as a query parameter so the anonymous, tenant-exempt
/// accept-invitation request can resolve the correct tenant schema (found during implementation:
/// TenantMiddleware has no JWT to resolve from on an unauthenticated route).
/// </summary>
internal static class StaffLinkBuilder
{
    public static string BuildInviteUrl(IConfiguration config, string token, string organisationSlug)
    {
        var inviteBase = config["App:StaffInviteBaseUrl"]
                       ?? $"{config["App:Scheme"] ?? "childcare"}://staff-invitation";
        return $"{inviteBase}?token={token}&org={organisationSlug}";
    }
}
