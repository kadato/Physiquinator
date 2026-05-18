namespace Physiquinator.Data;

/// <summary>JSON backup of workout session logs and set logs (see <see cref="WorkoutHistoryRepository"/>).</summary>
public sealed class WorkoutHistoryBackup
{
    public int FormatVersion { get; set; } = 1;

    public List<WorkoutHistoryBackupEntry> Sessions { get; set; } = new();
}

public sealed class WorkoutHistoryBackupEntry
{
    public WorkoutSessionLogEntity Session { get; set; } = null!;

    public List<WorkoutSetLogEntity> Sets { get; set; } = new();
}
