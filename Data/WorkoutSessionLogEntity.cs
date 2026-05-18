using SQLite;

namespace Physiquinator.Data;

[Table("WorkoutSessionLogs")]
public class WorkoutSessionLogEntity
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    public string WorkoutPlanId { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }
}
