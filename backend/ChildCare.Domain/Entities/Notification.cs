using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// Generic in-app notification-centre entry (research.md R4), modeled after
// ParentClosureMessage's shape but generalized across types instead of closure-specific.
// SourceId is intentionally polymorphic (MessageThread/Announcement/ChildEvent depending on
// Type) and carries NO database-level FK — see data-model.md's Notification entity note.
public class Notification
{
    public Guid             Id            { get; set; } = Guid.NewGuid();
    public Guid             TenantUserId  { get; set; }
    public NotificationType Type          { get; set; }
    public Guid             SourceId      { get; set; }
    public string           TitleKey      { get; set; } = string.Empty;
    public string           BodyKey       { get; set; } = string.Empty;
    public string           ArgumentsJson { get; set; } = "{}";
    public DateTime         CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime?        ReadAt        { get; set; }
}
