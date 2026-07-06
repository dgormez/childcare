namespace ChildCare.Domain.Entities;

public class StaffInvitation
{
    public Guid     Id            { get; set; } = Guid.NewGuid();
    public Guid     StaffProfileId { get; set; }
    public string   Email         { get; set; } = string.Empty;
    public byte[]   TokenHash     { get; set; } = [];
    public DateTime ExpiresAt     { get; set; }
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;

    // No UsedAt column: "used" is derived from whether the linked TenantUser.PasswordHash is
    // non-empty (research.md R2) — checked explicitly (not just ExpiresAt) so a second accept
    // attempt on an already-used, not-yet-expired token is still rejected (FR-006b).
}
