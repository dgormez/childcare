namespace ChildCare.Domain.Entities;

// developmental_milestones (data-model.md, feature 016), public schema — shared, platform-wide
// reference data (research.md R1). AgeFromMonths/AgeToMonths are inclusive on both ends
// (spec.md Assumptions).
public class DevelopmentalMilestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DomainId { get; set; }

    public int AgeFromMonths { get; set; }
    public int AgeToMonths { get; set; }

    public string DescriptionNl { get; set; } = string.Empty;
    public string DescriptionFr { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
