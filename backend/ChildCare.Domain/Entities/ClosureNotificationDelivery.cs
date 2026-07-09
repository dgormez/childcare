using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

public class ClosureNotificationDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClosureDayId { get; set; }
    public Guid ContactId { get; set; }
    public ClosureNotificationKind Kind { get; set; }
    public string? PushToken { get; set; }
    public ClosureDeliveryStatus PushStatus { get; set; } = ClosureDeliveryStatus.NotApplicable;
    public Guid? MessageId { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
