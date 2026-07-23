using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

public class StaffProfile
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    public Guid   TenantUserId { get; set; }
    public string FirstName    { get; set; } = string.Empty;
    public string LastName     { get; set; } = string.Empty;
    public string Phone        { get; set; } = string.Empty;

    // Required when the linked TenantUser.Role == Staff, optional when Role == Director
    // (FR-003) — enforced by the validator, not the schema (research.md R7).
    public QualificationLevel? QualificationLevel { get; set; }

    // GCS object path only, never a URL (research.md R3) — signed download URLs are generated
    // fresh on every read.
    public string? ProfilePhotoObjectPath { get; set; }

    // Soft-delete: null = active, non-null = deactivated. Cleared on reactivation.
    public DateTime? DeactivatedAt { get; set; }

    // Feature 008a (kiosk mode): bcrypt hash of this caregiver's 4-digit check-in PIN. Null
    // until a director sets one. Never the plaintext PIN, never logged.
    public string? PinHash { get; set; }

    // Sliding-window lockout for PinHash, anchored to the first failure in the current streak,
    // not a fixed clock-aligned window (spec FR-012, data-model.md). Viable as a simple
    // per-profile counter because select-then-PIN always gives VerifyPinCommand an explicit
    // staffId — there is never an anonymous failure to attribute (research.md R2).
    public int PinFailedAttempts { get; set; }
    public DateTime? PinFirstFailedAttemptAt { get; set; }
    public DateTime? PinLockedUntil { get; set; }

    // Feature 027 (data-model.md, research.md R2): which weekdays this staff member normally
    // works. Empty list = no restriction (safe default for pre-existing profiles). Drives the
    // grey-out behavior in both the director rota grid and the staff app's own schedule view.
    public List<DayOfWeek> ContractedDays { get; set; } = [];

    // Feature 027 deviation (not specified by data-model.md/research.md — flagged in the
    // implementation report): staff-mobile needs somewhere to register its Expo push token so
    // StaffScheduleNotificationService/StaffLeaveRequestNotificationService have a token to send
    // to. Mirrors Contact.PushToken's existing shape (feature 013b) — a single active token per
    // staff account, overwritten on each registration.
    public string? PushToken { get; set; }

    // Feature 028: which medewerkersbeleid function(s) this staff member may clock in under
    // (spec.md FR-010). Empty by default — a new staff member cannot clock in until a director
    // configures at least one (spec.md Key Entities).
    public List<StaffTimeEntryFunction> TimeEntryFunctions { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
