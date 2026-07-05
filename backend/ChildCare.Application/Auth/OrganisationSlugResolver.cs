using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Auth;

/// <summary>
/// Resolves a client-supplied organisation slug to its tenant (research.md R1), replacing
/// feature 002's "default tenant" shim. Used by every exempt-route auth command (login,
/// refresh, Google/Apple sign-in, forgot-password) to determine which tenant schema to
/// operate against before any TenantUser lookup happens (FR-008, FR-016). A slug that matches
/// no tenant, or one whose ProvisioningStatus isn't Ready, is deliberately indistinguishable
/// to the caller (FR-015) — slugs aren't secret, so this collapsing is about not leaking
/// provisioning state, not about account-existence privacy (that's AuthFailure.InvalidCredentials's job).
/// </summary>
public class OrganisationSlugResolver(IPublicDbContext publicDb)
{
    public async Task<Tenant?> ResolveAsync(string organisationSlug, CancellationToken cancellationToken = default)
    {
        var tenant = await publicDb.Tenants
            .FirstOrDefaultAsync(t => t.Slug == organisationSlug, cancellationToken);

        return tenant is { ProvisioningStatus: ProvisioningStatus.Ready } ? tenant : null;
    }
}
