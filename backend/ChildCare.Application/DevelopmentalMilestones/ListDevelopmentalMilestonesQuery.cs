using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DevelopmentalMilestones;

// Reads IPublicDbContext directly (research.md R1, mirrors ListVaccineTypesQuery) —
// developmental_domains/developmental_milestones is shared, platform-wide reference data, not
// tenant-scoped, so this is not a TenantMiddleware bypass.
public record ListDevelopmentalMilestonesQuery : IRequest<IReadOnlyList<DevelopmentalDomainResponse>>;

public class ListDevelopmentalMilestonesQueryHandler(IPublicDbContext publicDb)
    : IRequestHandler<ListDevelopmentalMilestonesQuery, IReadOnlyList<DevelopmentalDomainResponse>>
{
    public async Task<IReadOnlyList<DevelopmentalDomainResponse>> Handle(ListDevelopmentalMilestonesQuery request, CancellationToken cancellationToken)
    {
        var domains = await publicDb.DevelopmentalDomains.AsNoTracking().ToListAsync(cancellationToken);
        var milestones = await publicDb.DevelopmentalMilestones.AsNoTracking().ToListAsync(cancellationToken);

        return DevelopmentalMilestoneMapper.ToCatalogResponse(domains, milestones);
    }
}
