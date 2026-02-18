namespace Physiquinator.Models;

public class WorkoutPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Workout";
    public List<ExercisePlan> Exercises { get; set; } = new();
    /// <summary>Rest interval in seconds between sets.</summary>
    public int RestIntervalSeconds { get; set; } = 60;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
