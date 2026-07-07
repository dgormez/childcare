namespace ChildCare.Domain.Entities;

public class VaccinationRecord
{
    public Guid     Id               { get; set; } = Guid.NewGuid();
    public Guid     ChildId          { get; set; }
    public string   VaccineName      { get; set; } = string.Empty;
    public DateOnly DateAdministered { get; set; }
    public DateOnly? NextDueDate     { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
