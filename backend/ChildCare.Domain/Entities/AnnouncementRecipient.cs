namespace ChildCare.Domain.Entities;

// One row per contact the announcement actually reached (research.md R8 — bounded to contacts
// with an active parent account). Per-recipient read state, unlike Message's shared marker,
// since an announcement is one-to-many rather than a two-party conversation.
public class AnnouncementRecipient
{
    public Guid     Id             { get; set; } = Guid.NewGuid();
    public Guid     AnnouncementId { get; set; }
    public Guid     ContactId      { get; set; }
    public DateTime? ReadAt        { get; set; }
}
