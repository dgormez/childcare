namespace ChildCare.Domain.Entities;

// Structural copy of StaffInvitation (feature 005) — see
// specs/013-parent-communication/research.md R1 for why a third concrete invitation table,
// rather than generalizing Invitation/StaffInvitation, is the consistent move here.
public class ParentInvitation
{
    public Guid     Id        { get; set; } = Guid.NewGuid();
    public Guid     ContactId { get; set; }
    public string   Email     { get; set; } = string.Empty;
    public byte[]   TokenHash { get; set; } = [];
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // No UsedAt column: "used" is derived from whether the linked TenantUser.PasswordHash is
    // non-empty, same reasoning as StaffInvitation/Invitation.
}
