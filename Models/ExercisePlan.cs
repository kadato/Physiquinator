namespace Physiquinator.Models;

public class ExercisePlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int SetCount { get; set; } = 1;
    public int Order { get; set; }
}
