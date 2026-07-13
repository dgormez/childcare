using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

public class Child
{
    public Guid     Id          { get; set; } = Guid.NewGuid();
    public string   FirstName   { get; set; } = string.Empty;
    public string   LastName    { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }

    // GCS object path only, never a URL (research.md R1) — signed download URLs are generated
    // fresh on every read.
    public string? ProfilePhotoObjectPath { get; set; }

    public Gender? Gender      { get; set; }
    public string? Nationality { get; set; }

    // Medical information — all nullable, never blocks file creation (FR-003).
    public string?          AllergiesDescription  { get; set; }
    public AllergySeverity? AllergySeverity        { get; set; }
    public string?          MedicalConditions      { get; set; }
    public string?          DietaryRestrictions    { get; set; }
    public string?          GpName                 { get; set; }
    public string?          GpPhone                { get; set; }
    public string?          PediatricianName       { get; set; }
    public string?          PediatricianPhone      { get; set; }
    public string?          HealthInsuranceNumber  { get; set; }

    // Opgroeien child identifier (YYMMDD-NNN) — stored as entered, not format-validated in
    // Phase 1 (FR-009).
    public string? Kindcode { get; set; }

    // Soft-delete: null = active, non-null = deactivated. Cleared on reactivation.
    public DateTime? DeactivatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
