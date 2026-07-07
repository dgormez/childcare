namespace ChildCare.Domain.Entities;

// Dated history — assigning a new group never deletes or overwrites a prior row, only sets its
// EndDate (FR-008a).
public class ChildGroupAssignment
{
    public Guid     Id        { get; set; } = Guid.NewGuid();
    public Guid     ChildId   { get; set; }
    public Guid     GroupId   { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate  { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
