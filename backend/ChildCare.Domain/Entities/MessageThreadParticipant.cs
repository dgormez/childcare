namespace ChildCare.Domain.Entities;

// Composite key (ThreadId, TenantUserId) — TenantUserId uniformly covers parent, staff, and
// director participants (research.md R6), not a mix of Contact.Id and TenantUser.Id.
public class MessageThreadParticipant
{
    public Guid     ThreadId     { get; set; }
    public Guid     TenantUserId { get; set; }
    public DateTime AddedAt      { get; set; } = DateTime.UtcNow;
}
