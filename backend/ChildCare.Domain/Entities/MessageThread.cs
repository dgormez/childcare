namespace ChildCare.Domain.Entities;

public class MessageThread
{
    public Guid     Id             { get; set; } = Guid.NewGuid();
    public string   Subject        { get; set; } = string.Empty;

    // Null = general, non-child-specific thread (spec.md FR-003).
    public Guid?    ChildId        { get; set; }

    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;

    // Denormalized for "most recently active first" ordering (spec.md User Story 2, Scenario 3)
    // — updated on every new message rather than computed via a join at read time.
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}
