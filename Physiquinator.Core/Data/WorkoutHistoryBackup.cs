namespace Physiquinator.Core.Data;

/// <summary>JSON backup of workout session logs, set logs, and bodyweight logs (see <see cref="WorkoutHistoryRepository"/>).</summary>
public sealed class WorkoutHistoryBackup
{
    public int FormatVersion { get; set; } = 1;

    public List<WorkoutHistoryBackupEntry> Sessions { get; set; } = [];

    public List<BodyweightLogEntity> BodyweightEntries { get; set; } = [];
}

public sealed class WorkoutHistoryBackupEntry
{
    public WorkoutSessionLogEntity Session { get; set; } = null!;

    public List<WorkoutSetLogEntity> Sets { get; set; } = [];
}
