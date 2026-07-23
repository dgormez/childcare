using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Organisations;

// research.md R5: answerable entirely from the Public schema (Tenant left-joined to Invitation
// on CreatedFromInvitationId) — no per-tenant-schema fan-out, since every field shown already
// lives in shared, cross-tenant data.
public record ListPlatformAdminOrganisationsQuery : IRequest<IReadOnlyList<PlatformAdminOrganisationResponse>>;

public class ListPlatformAdminOrganisationsQueryHandler(IPublicDbContext publicDb)
    : IRequestHandler<ListPlatformAdminOrganisationsQuery, IReadOnlyList<PlatformAdminOrganisationResponse>>
{
    public async Task<IReadOnlyList<PlatformAdminOrganisationResponse>> Handle(ListPlatformAdminOrganisationsQuery request, CancellationToken cancellationToken)
    {
        var rows = await (
            from tenant in publicDb.Tenants
            join invitation in publicDb.Invitations
                on tenant.CreatedFromInvitationId equals invitation.Id into invitationJoin
            from invitation in invitationJoin.DefaultIfEmpty()
            orderby tenant.CreatedAt descending
            select new { tenant, RegisteredByEmail = invitation != null ? invitation.Email : null }
        ).ToListAsync(cancellationToken);

        return rows.Select(r => new PlatformAdminOrganisationResponse(
            r.tenant.Id,
            r.tenant.Name,
            r.tenant.Plan.ToString().ToLowerInvariant(),
            r.tenant.ProvisioningStatus.ToString().ToLowerInvariant(),
            r.tenant.KboNumber,
            r.tenant.CreatedAt,
            r.RegisteredByEmail)).ToList();
    }
}
