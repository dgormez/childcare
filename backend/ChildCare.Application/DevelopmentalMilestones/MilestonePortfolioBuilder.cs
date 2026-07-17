using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.DevelopmentalMilestones;

// Shared age-band resolution + domain-grouping logic (research.md R2) — used by the director
// portfolio query, the parent portfolio query, and the PDF export, so all three resolve
// age-appropriateness and "current status" identically. Pure computation over already-loaded
// data; no DB access of its own.
public static class MilestonePortfolioBuilder
{
    public static MilestonePortfolioResponse Build(
        Guid childId,
        DateOnly childDateOfBirth,
        DateOnly today,
        IReadOnlyList<DevelopmentalDomain> domains,
        IReadOnlyList<DevelopmentalMilestone> milestones,
        IReadOnlyList<ChildMilestoneObservation> observations,
        bool includeHistory)
    {
        var ageInMonths = AgeInMonths(childDateOfBirth, today);

        var observationsByMilestone = observations
            .GroupBy(o => o.MilestoneId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.CreatedAt).ToList());

        var domainResponses = domains
            .OrderBy(d => d.SortOrder)
            .Select(domain =>
            {
                var milestoneResponses = milestones
                    .Where(m => m.DomainId == domain.Id)
                    .OrderBy(m => m.SortOrder)
                    .Select(milestone => BuildMilestoneResponse(milestone, ageInMonths, observationsByMilestone, includeHistory))
                    .ToList();

                return new DevelopmentalDomainResponse(
                    domain.Id, domain.Code, domain.NameNl, domain.NameFr, domain.NameEn, domain.SortOrder, milestoneResponses);
            })
            .ToList();

        return new MilestonePortfolioResponse(childId, domainResponses);
    }

    private static DevelopmentalMilestoneResponse BuildMilestoneResponse(
        DevelopmentalMilestone milestone,
        int ageInMonths,
        IReadOnlyDictionary<Guid, List<ChildMilestoneObservation>> observationsByMilestone,
        bool includeHistory)
    {
        // Inclusive both ends (spec.md Assumptions).
        var isCurrentFocus = ageInMonths >= milestone.AgeFromMonths && ageInMonths <= milestone.AgeToMonths;

        observationsByMilestone.TryGetValue(milestone.Id, out var observations);
        var current = observations?.FirstOrDefault(); // already ordered newest-first

        IReadOnlyList<MilestoneObservationResponse>? history = includeHistory
            ? (observations ?? []).Select(ToResponse).ToList()
            : null;

        return new DevelopmentalMilestoneResponse(
            milestone.Id,
            milestone.AgeFromMonths,
            milestone.AgeToMonths,
            milestone.DescriptionNl,
            milestone.DescriptionFr,
            milestone.DescriptionEn,
            milestone.SortOrder,
            current?.Status.ToWireString(),
            isCurrentFocus,
            history);
    }

    private static MilestoneObservationResponse ToResponse(ChildMilestoneObservation observation) =>
        new(observation.Id, observation.Status.ToWireString(), observation.ObservedAt, observation.Notes, observation.CreatedAt);

    private static int AgeInMonths(DateOnly dateOfBirth, DateOnly today)
    {
        var months = ((today.Year - dateOfBirth.Year) * 12) + (today.Month - dateOfBirth.Month);
        if (today.Day < dateOfBirth.Day)
            months--;
        return Math.Max(0, months);
    }
}
