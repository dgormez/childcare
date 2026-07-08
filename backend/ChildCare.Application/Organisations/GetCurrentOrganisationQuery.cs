using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Organisations;

/// <summary>
/// Feature 007a: exposes the current tenant's display name to the client. No existing endpoint
/// returned this (only TenantSlug is resolved by TenantMiddleware, and never surfaced to the
/// client) — see spec.md FR-005a.
/// </summary>
public record GetCurrentOrganisationQuery : IRequest<OrganisationResponse>;

public class GetCurrentOrganisationQueryHandler(IPublicDbContext publicDb, ICurrentTenantService currentTenant)
    : IRequestHandler<GetCurrentOrganisationQuery, OrganisationResponse>
{
    public async Task<OrganisationResponse> Handle(GetCurrentOrganisationQuery request, CancellationToken cancellationToken)
    {
        var tenant = await publicDb.Tenants.FirstAsync(t => t.Id == currentTenant.TenantId, cancellationToken);
        return new OrganisationResponse(tenant.Name);
    }
}
