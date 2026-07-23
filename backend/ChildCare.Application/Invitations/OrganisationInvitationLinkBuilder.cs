using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.Invitations;

/// <summary>
/// Builds the registration link emailed for an organisation invitation (feature 032,
/// research.md R8). Unlike StaffLinkBuilder/ParentLinkBuilder's `?token=...&amp;org=...` shape,
/// this carries only the token — no tenant/organisation exists yet to resolve a schema for, and
/// the invitation is looked up by token hash alone in the Public schema
/// (RegisterOrganisationCommandHandler). Also unlike AuthLinkBuilder's app-scheme deep link, this
/// is a browser-based director-web page, so it's a plain http(s) URL, mirroring
/// EnrollmentLinkBuilder's `App:PublicEnrollmentBaseUrl` convention.
/// </summary>
internal static class OrganisationInvitationLinkBuilder
{
    public static string BuildRegisterUrl(IConfiguration config, string token)
    {
        var baseUrl = config["App:OrganisationRegisterBaseUrl"] ?? "http://localhost:3000/register";
        return $"{baseUrl.TrimEnd('/')}?token={Uri.EscapeDataString(token)}";
    }
}
