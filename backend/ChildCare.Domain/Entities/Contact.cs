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

    // Expo push token (feature 009) — nullable, never populated by any client yet since no
    // parent-facing registration path exists (accepted gap, spec.md Assumptions). Exists now so
    // the temperature-alert recipient query is real/testable ahead of that registration UI.
    public string? PushToken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
