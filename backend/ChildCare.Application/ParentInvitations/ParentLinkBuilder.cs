using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.ParentInvitations;

/// <summary>
/// Builds the deep link emailed for a parent invitation, mirroring StaffLinkBuilder (feature
/// 005) — carries the organisation slug as a query parameter so the anonymous, tenant-exempt
/// accept-invitation request can resolve the correct tenant schema.
/// </summary>
internal static class ParentLinkBuilder
{
    public static string BuildInviteUrl(IConfiguration config, string token, string organisationSlug)
    {
        var inviteBase = config["App:ParentInviteBaseUrl"]
                       ?? $"{config["App:ParentScheme"] ?? "childcareparent"}://parent-invitation";
        return $"{inviteBase}?token={token}&org={organisationSlug}";
    }
}
