namespace ChildCare.Domain.Entities;

public class Message
{
    public Guid     Id       { get; set; } = Guid.NewGuid();
    public Guid     ThreadId { get; set; }
    public Guid     SenderId { get; set; }
    public string   Body     { get; set; } = string.Empty;
    public DateTime SentAt   { get; set; } = DateTime.UtcNow;

    // "Read by the other side" marker (research.md R7), not a per-participant read receipt:
    // for a parent-authored message, set on the first director/staff read; for a
    // staff-authored message, set on the first read by any parent participant on the thread.
    public DateTime? ReadAt  { get; set; }
}
