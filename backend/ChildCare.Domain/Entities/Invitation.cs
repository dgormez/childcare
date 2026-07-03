namespace ChildCare.Domain.Entities;

public class Invitation
{
    public Guid     Id          { get; set; } = Guid.NewGuid();
    public string   Email       { get; set; } = string.Empty;
    public byte[]   TokenHash   { get; set; } = [];
    public DateTime ExpiresAt   { get; set; }
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;

    // No UsedAt column: "used" is derived from whether a ready Tenant exists
    // with CreatedFromInvitationId pointing at this invitation (research.md R10).
}
