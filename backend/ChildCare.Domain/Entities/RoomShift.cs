namespace ChildCare.Domain.Entities;

/// <summary>
/// A single caregiver's presence window in a room (feature 008a, kiosk mode) — identity and
/// accountability, not HTTP authentication. Multiple caregivers can have simultaneous open
/// shifts for the same location/group; this is the expected norm (spec FR-011), not an
/// exceptional state.
/// </summary>
public class RoomShift
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffProfileId { get; set; }
    public Guid LocationId { get; set; }
    public Guid GroupId { get; set; }
    public Guid DevicePairingId { get; set; }

    public DateTime CheckedInAt { get; set; }

    // Null = still open.
    public DateTime? CheckedOutAt { get; set; }

    // null (explicit check-out) | "auto_checkout" | "deactivated" | "reassigned" — lets a
    // director's correction distinguish an intentional check-out from a system-closed one
    // (spec FR-023/024/026).
    public string? ClosedReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
