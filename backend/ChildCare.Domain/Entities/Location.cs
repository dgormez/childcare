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

    // Soft-delete: null = active, non-null = deactivated. Cleared on reactivation.
    public DateTime? DeactivatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
