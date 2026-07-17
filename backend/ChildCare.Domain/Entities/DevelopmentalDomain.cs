namespace ChildCare.Domain.Entities;

// developmental_domains (data-model.md, feature 016), public schema — shared, platform-wide
// reference data, identical across every tenant (research.md R1, mirrors VaccineType's
// precedent). A fixed, closed set of 7 domains — no deactivation path in this feature.
public class DevelopmentalDomain
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;
    public string NameNl { get; set; } = string.Empty;
    public string NameFr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
