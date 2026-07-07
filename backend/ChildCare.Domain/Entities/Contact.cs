namespace ChildCare.Domain.Entities;

// Standalone person record — no TenantUserId/account link (spec.md Assumptions: contacts are
// data records only in this feature). Never deleted; a contact simply accumulates/loses
// ChildContact links over time.
public class Contact
{
    public Guid   Id        { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName  { get; set; } = string.Empty;
    public string Phone     { get; set; } = string.Empty;
    public string? Email    { get; set; }
    public string Locale    { get; set; } = "nl";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
