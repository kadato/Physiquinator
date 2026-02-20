using SQLite;

namespace Physiquinator.Data;

[Table("ExercisePlans")]
public class ExercisePlanEntity
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;
    
    [Indexed]
    public string WorkoutPlanId { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    
    public int SetCount { get; set; }
    
    public int Order { get; set; }
    
    public int RestIntervalSeconds { get; set; }
}
