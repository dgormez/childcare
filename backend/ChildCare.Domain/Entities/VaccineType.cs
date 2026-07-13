using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// vaccine_types (data-model.md, feature 013g), public schema — shared, platform-wide reference
// data, identical across every tenant (research.md R1). Soft-delete only via IsActive; never
// hard-deleted (existing VaccineRecords may still reference a retired entry).
public class VaccineType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public VaccineCategory? Category { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
