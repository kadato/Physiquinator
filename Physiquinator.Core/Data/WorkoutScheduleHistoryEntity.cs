using SQLite;

namespace Physiquinator.Core.Data;

[Table("WorkoutScheduleHistory")]
public class WorkoutScheduleHistoryEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Day-of-week bitmask (Sunday = bit 0 .. Saturday = bit 6).
    /// </summary>
    public int DaysBitmask { get; set; }

    /// <summary>
    /// Local calendar date in yyyy-MM-dd format from which this schedule is active.
    /// </summary>
    [Indexed]
    public string EffectiveFrom { get; set; } = string.Empty;
}
