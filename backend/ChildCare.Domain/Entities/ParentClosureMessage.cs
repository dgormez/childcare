using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

public class ParentClosureMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContactId { get; set; }
    public Guid ClosureDayId { get; set; }
    public ClosureNotificationKind Kind { get; set; }
    public string TitleKey { get; set; } = string.Empty;
    public string BodyKey { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
