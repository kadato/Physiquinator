using SQLite;

namespace Physiquinator.Data;

[Table("WorkoutPlans")]
public class WorkoutPlanEntity
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    
    public int RestIntervalSeconds { get; set; }
    
    public int DefaultSetCount { get; set; }
    
    public DateTime CreatedAt { get; set; }
}
