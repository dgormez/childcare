namespace ChildCare.Domain.Entities;

/// <summary>
/// The room-scoped hardware identity of a paired caregiver tablet (feature 008a, kiosk mode).
/// <see cref="Id"/> is the <c>device_id</c> claim embedded in every device token issued for
/// this pairing.
/// </summary>
public class DevicePairing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public Guid GroupId { get; set; }

    // bcrypt hash of the 6-digit director-override PIN set during pairing.
    public string DirectorOverridePinHash { get; set; } = string.Empty;

    // Start of the current 30-day TTL window.
    public DateTime TokenIssuedAt { get; set; }

    // Incremented on every rotation (research.md R3) — a stale, pre-rotation token is
    // naturally rejected without needing a separate revocation-list entry per rotation.
    public int TokenVersion { get; set; } = 1;

    // Null = active. Checked on every request, not only at token-issuance time (FR-021).
    public DateTime? RevokedAt { get; set; }

    public Guid PairedByTenantUserId { get; set; }

    // Sliding-window lockout for the director-override PIN — a single-target comparison
    // against DirectorOverridePinHash, unrelated to caregiver-PIN lockout on StaffProfile
    // (spec FR-005).
    public int OverridePinFailedAttempts { get; set; }
    public DateTime? OverridePinFirstFailedAttemptAt { get; set; }
    public DateTime? OverridePinLockedUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
