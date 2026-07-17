using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.DevelopmentalMilestones;

// Plain catalog listing (ListDevelopmentalMilestonesQuery) — no child/observation context, so
// CurrentStatus/IsCurrentFocus/History are always null/false/null. MilestonePortfolioBuilder
// handles the child-scoped, observation-aware shape.
public static class DevelopmentalMilestoneMapper
{
    public static IReadOnlyList<DevelopmentalDomainResponse> ToCatalogResponse(
        IReadOnlyList<DevelopmentalDomain> domains, IReadOnlyList<DevelopmentalMilestone> milestones) =>
        domains
            .OrderBy(d => d.SortOrder)
            .Select(domain => new DevelopmentalDomainResponse(
                domain.Id, domain.Code, domain.NameNl, domain.NameFr, domain.NameEn, domain.SortOrder,
                milestones
                    .Where(m => m.DomainId == domain.Id)
                    .OrderBy(m => m.SortOrder)
                    .Select(m => new DevelopmentalMilestoneResponse(
                        m.Id, m.AgeFromMonths, m.AgeToMonths, m.DescriptionNl, m.DescriptionFr, m.DescriptionEn,
                        m.SortOrder, CurrentStatus: null, IsCurrentFocus: false, History: null))
                    .ToList()))
            .ToList();
}
