namespace ChildCare.Domain.Entities;

public class Location
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public string Name        { get; set; } = string.Empty;
    public string Address     { get; set; } = string.Empty;
    public string Phone       { get; set; } = string.Empty;
    public string Email       { get; set; } = string.Empty;
    public int    MaxCapacity { get; set; }

    // Opgroeien reporting settings (nullable — filled in later, not required at creation)
    public string? NaamLocatie       { get; set; }
    public string? Dossiernummer     { get; set; }
    public string? Verantwoordelijke { get; set; }
    public bool    FlexPermission    { get; set; } = false;
    public bool    BoPermission      { get; set; } = false;

    // Soft-delete: null = active, non-null = deactivated. Cleared on reactivation.
    public DateTime? DeactivatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
