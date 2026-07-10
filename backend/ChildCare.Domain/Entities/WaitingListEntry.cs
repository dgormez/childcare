using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

public class WaitingListEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ChildFirstName { get; set; } = string.Empty;
    public string ChildLastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly? RequestedStartDate { get; set; }
    public int Priority { get; set; }
    public WaitingListStatus Status { get; set; } = WaitingListStatus.Waiting;
    public string? Notes { get; set; }

    // Set only via LinkChildToWaitingListEntryCommand (FR-010/FR-011), never auto-matched.
    public Guid? ChildId { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
