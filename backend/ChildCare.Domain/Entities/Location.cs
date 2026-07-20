using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

public class Location
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public string Name        { get; set; } = string.Empty;
    public string Address     { get; set; } = string.Empty;
    public string Phone       { get; set; } = string.Empty;
    public string Email       { get; set; } = string.Empty;
    public int    MaxCapacity { get; set; }

    // Opgroeien reporting settings (nullable — filled in later, not required at creation)
    public string? NaamLocatie       { get; set; }
    public string? Dossiernummer     { get; set; }
    public string? Verantwoordelijke { get; set; }
    public bool    FlexPermission    { get; set; } = false;
    public bool    BoPermission      { get; set; } = false;

    // Feature 013f — per-location day-reservation (013a) policy. Defaults mirror 013a's
    // original fixed behavior exactly, so a location that predates this feature is unaffected.
    public ReservationRequestMode ReservationAbsencesMode { get; set; } = ReservationRequestMode.Approval;
    public ReservationRequestMode ReservationExtrasMode { get; set; } = ReservationRequestMode.Approval;
    public ReservationRequestMode ReservationSwapsMode { get; set; } = ReservationRequestMode.Disabled;
    public int ReservationNoticeHours { get; set; } = 0;

    // Feature 008b — whether caregiver PIN verification is required at check-in/check-out for
    // this location. Defaults to true so no existing location's behavior changes until a
    // director explicitly opts out.
    public bool RequiresCaregiverPin { get; set; } = true;

    // Feature 013j — ordered set of DietaryType wire strings this location offers alongside the
    // base monthly menu. Order IS the priority order used to resolve a child matching more than
    // one enabled type (spec.md FR-002/FR-008) — not just set membership, unlike
    // MealPreference.DietaryType. Empty by default so a location that predates this feature
    // behaves identically to before (FR-012). Deliberately List<string> (wire strings, parsed
    // where needed via DietaryTypeExtensions.TryParseWireString), not List<DietaryType> — a
    // second List<DietaryType>-shaped EF value converter in the same model as MonthlyMenu.
    // Variant's DietaryType? converter triggers a Npgsql.EntityFrameworkCore.PostgreSQL provider
    // bug in its array-conversion machinery (research.md: "Location.MenuVariantPriorityOrder
    // storage" decision). The JSON wire contract (string[]) is unaffected either way.
    public List<string> MenuVariantPriorityOrder { get; set; } = [];

    // Feature 014 — per-location invoicing details. Erkenningsnummer/BankAccountNumber are
    // per-location (not per-organisation) because a multi-location organisation can have a
    // distinct childcare license and bank account per physical site (spec.md Clarifications).
    public string? Erkenningsnummer  { get; set; }
    public string? BankAccountNumber { get; set; }
    public int      InvoiceDueDays   { get; set; } = 14;

    // Feature 014a — per-location automatic payment-reminder settings (spec.md FR-012).
    // Disabled by default so a location that never configures this sees no behavior change
    // (spec.md Assumptions/SC-005). Delay/cadence default to 3/7 days once enabled.
    public bool PaymentRemindersEnabled    { get; set; } = false;
    public int  PaymentReminderDelayDays   { get; set; } = 3;
    public int  PaymentReminderCadenceDays { get; set; } = 7;

    // Feature 030 — opt-in sibling billing (spec.md FR-004/FR-007). Both default to no-op so a
    // location that never configures them sees zero invoice-generation behavior change
    // (spec.md SC-005).
    public decimal SiblingDiscountPct             { get; set; } = 0;
    public bool    FamilyInvoiceBundlingEnabled    { get; set; } = false;

    // Feature 021 — whether QR contactless check-in is available at this location (spec.md
    // FR-001/FR-002). Defaults to false so no location's behavior changes until a director
    // explicitly opts in.
    public bool QrCheckInEnabled { get; set; } = false;

    // Soft-delete: null = active, non-null = deactivated. Cleared on reactivation.
    public DateTime? DeactivatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
