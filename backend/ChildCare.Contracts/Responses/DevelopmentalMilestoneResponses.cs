namespace ChildCare.Contracts.Responses;

// Feature 016 — contracts/developmental-milestones-api.md.
public record DevelopmentalDomainResponse(
    Guid Id,
    string Code,
    string NameNl,
    string NameFr,
    string NameEn,
    int SortOrder,
    IReadOnlyList<DevelopmentalMilestoneResponse> Milestones);

// History is null on the parent-facing portfolio response (data-model.md's Derived View note —
// parents see current status only, not the full observation-by-observation history) and
// populated on the director-facing one.
public record DevelopmentalMilestoneResponse(
    Guid Id,
    int AgeFromMonths,
    int AgeToMonths,
    string DescriptionNl,
    string DescriptionFr,
    string DescriptionEn,
    int SortOrder,
    string? CurrentStatus,
    bool IsCurrentFocus,
    IReadOnlyList<MilestoneObservationResponse>? History);

public record MilestoneObservationResponse(
    Guid Id,
    string Status,
    DateOnly ObservedAt,
    string? Notes,
    DateTime CreatedAt);

public record MilestonePortfolioResponse(
    Guid ChildId,
    IReadOnlyList<DevelopmentalDomainResponse> Domains);
