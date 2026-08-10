using SQLite;

namespace Physiquinator.Core.Data;

[Table("ExercisePlans")]
public class ExercisePlanEntity
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string WorkoutPlanId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SetCount { get; set; }

    public int WarmupSetCount { get; set; }

    public string? SupersetGroupId { get; set; }

    public int Order { get; set; }

    public int RestIntervalSeconds { get; set; }

    public int? DefaultReps { get; set; }

    public double? DefaultWeightKg { get; set; }

    public double? BodyweightPercent { get; set; }

    public int LogType { get; set; }
}
