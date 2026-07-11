using SQLite;

namespace Physiquinator.Data;

[Table("WorkoutSessionLogs")]
public class WorkoutSessionLogEntity
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string WorkoutPlanId { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    [Indexed]
    public DateTime StartedAtUtc { get; set; }

    [Indexed]
    public DateTime? EndedAtUtc { get; set; }

    /// <summary>JSON snapshot of the workout plan at session start.</summary>
    public string? PlanSnapshotJson { get; set; }
}
