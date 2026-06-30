namespace ChildCare.Api.Models;

public class Habit
{
    public Guid   Id        { get; set; } = Guid.NewGuid();
    public Guid   UserId    { get; set; }
    public User   User      { get; set; } = null!;
    public string Name      { get; set; } = string.Empty;
    public string Color     { get; set; } = "#3b82f6";
    public string Icon      { get; set; } = "✅";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<HabitCompletion> Completions { get; set; } = [];
}
