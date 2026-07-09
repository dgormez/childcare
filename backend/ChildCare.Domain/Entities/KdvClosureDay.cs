using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

public class KdvClosureDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LocationId { get; set; }
    public DateOnly Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public ClosureType ClosureType { get; set; } = ClosureType.Holiday;
    public bool NotifyParents { get; set; } = true;
    public ClosureStatus Status { get; set; } = ClosureStatus.Draft;
    public DateTime? NotificationSentAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? AttendanceGeneratedAt { get; set; }
    public Guid? AttendanceGeneratedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
