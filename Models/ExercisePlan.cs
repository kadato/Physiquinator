namespace Physiquinator.Models;

public class ExercisePlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int SetCount { get; set; } = 4;
    public int Order { get; set; }
    /// <summary>Rest interval in seconds after completing a set of this exercise.</summary>
    public int RestIntervalSeconds { get; set; } = 60;
}
