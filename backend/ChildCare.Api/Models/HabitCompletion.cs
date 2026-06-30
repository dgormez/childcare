namespace ChildCare.Api.Models;

public class HabitCompletion
{
    public Guid     Id        { get; set; } = Guid.NewGuid();
    public Guid     HabitId   { get; set; }
    public Habit    Habit     { get; set; } = null!;
    public Guid     UserId    { get; set; }
    public DateOnly Date      { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
