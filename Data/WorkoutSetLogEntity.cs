using SQLite;

namespace Physiquinator.Data;

[Table("WorkoutSetLogs")]
public class WorkoutSetLogEntity
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string SessionId { get; set; } = string.Empty;

    public int ExerciseIndex { get; set; }

    public string ExerciseName { get; set; } = string.Empty;

    public int SetIndex { get; set; }

    public DateTime CompletedAtUtc { get; set; }

    public int? Reps { get; set; }

    public double? WeightKg { get; set; }
}
