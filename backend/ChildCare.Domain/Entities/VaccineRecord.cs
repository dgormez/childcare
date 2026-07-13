namespace ChildCare.Domain.Entities;

// vaccine_records (data-model.md, feature 013c) — replaces the unused vaccination_records table
// from feature 006 (research.md R1). RecordedBy is nullable only so rows backfilled from the
// legacy table (which never captured an actor) have a valid representation.
public class VaccineRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }

    public string VaccineName { get; set; } = string.Empty;
    public int? DoseNumber { get; set; }
    public DateOnly AdministeredOn { get; set; }
    public DateOnly? NextDueDate { get; set; }
    public string? AdministeredBy { get; set; }
    public string? Notes { get; set; }

    // Feature 013g — mutually exclusive (never both, DB-enforced via CHECK constraint,
    // data-model.md). No DB FK on VaccineTypeId: VaccineType is soft-delete-only, so the one
    // failure mode a FK guards against can't occur (research.md R2).
    public Guid? VaccineTypeId { get; set; }
    public Guid? CustomVaccineEntryId { get; set; }
    public string? AttachmentObjectPath { get; set; }

    public Guid? RecordedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Soft-delete: null = active.
    public DateTime? DeletedAt { get; set; }
}
