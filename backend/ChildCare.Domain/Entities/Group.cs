namespace ChildCare.Domain.Entities;

// No deactivation state in Phase 1.
public class Group
{
    public Guid   Id         { get; set; } = Guid.NewGuid();
    public Guid   LocationId { get; set; }
    public string Name       { get; set; } = string.Empty;

    // Feature 018 — number of children this group is designed to hold, used to colour-code its
    // occupancy (spec.md FR-001). Null until a director sets one; existing groups predate this
    // field and show headcount only, with no capacity ratio or colour, until then (Edge Cases).
    // Supersedes this file's prior "no capacity" note — BACKLOG.md's per-group colour-coded
    // occupancy requirement has no reasonable implementation without it (spec.md Assumptions).
    public int? Capacity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
