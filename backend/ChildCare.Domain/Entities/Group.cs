namespace ChildCare.Domain.Entities;

// Minimal — no capacity, no BKR configuration (spec.md Assumptions, research.md R2). No
// deactivation state in Phase 1.
public class Group
{
    public Guid   Id         { get; set; } = Guid.NewGuid();
    public Guid   LocationId { get; set; }
    public string Name       { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
