namespace ChildCare.Domain.Entities;

// tenant_custom_vaccine_entries (data-model.md, feature 013g), tenant schema — a KDV-scoped
// memory of vaccine names directors have typed that matched no VaccineType catalog entry
// (research.md R3). NormalizedName is case/whitespace/diacritic-folded at write time
// (CustomVaccineEntryResolver) and uniquely indexed per tenant schema to dedupe under
// concurrent writes, not just sequential ones.
public class TenantCustomVaccineEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
