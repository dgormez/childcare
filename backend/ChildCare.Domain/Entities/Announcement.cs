namespace ChildCare.Domain.Entities;

public class Announcement
{
    public Guid     Id                 { get; set; } = Guid.NewGuid();
    public Guid     LocationId         { get; set; }

    // Null = whole-location scope; set = group-scoped (spec.md FR-007).
    public Guid?    GroupId            { get; set; }

    public string   Subject            { get; set; } = string.Empty;
    public string   Body               { get; set; } = string.Empty;
    public Guid     SentByTenantUserId { get; set; }
    public DateTime SentAt             { get; set; } = DateTime.UtcNow;
}
