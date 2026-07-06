namespace ChildCare.Domain.Entities;

/// <summary>
/// Join entity — which locations (feature 004) a staff member is eligible to work at.
/// Composite PK (StaffProfileId, LocationId), no surrogate Id. Carries no date/schedule
/// information — that belongs to feature 011.
/// </summary>
public class StaffLocationEligibility
{
    public Guid     StaffProfileId { get; set; }
    public Guid     LocationId     { get; set; }
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;
}
