namespace Physiquinator.Core.Models;

public class ExercisePlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int SetCount { get; set; } = 4;

    /// <summary>Number of warm-up sets logged before the working sets.</summary>
    public int WarmupSetCount { get; set; }

    /// <summary>
    /// Shared identifier linking this exercise to others in a superset or
    /// circuit: no rest is taken between sets of exercises in the same group.
    /// Null when the exercise stands alone.
    /// </summary>
    public string? SupersetGroupId { get; set; }

    public int Order { get; set; }
    /// <summary>Rest interval in seconds after completing a set of this exercise.</summary>
    public int RestIntervalSeconds { get; set; } = 60;

    /// <summary>Optional default reps shown when logging a set for this exercise.</summary>
    public int? DefaultReps { get; set; }

    /// <summary>Optional default load in kilograms for set logging.</summary>
    public double? DefaultWeightKg { get; set; }

    /// <summary>
    /// Share of the user's bodyweight counted toward volume for bodyweight
    /// exercises, in percent, for example 65 for push-ups. Null means full
    /// bodyweight (100%). Only used when <see cref="LogType"/> is
    /// <see cref="ExerciseLogType.BodyweightReps"/>.
    /// </summary>
    public double? BodyweightPercent { get; set; }

    /// <summary>The logging style used for this exercise.</summary>
    public ExerciseLogType LogType { get; set; } = ExerciseLogType.WeightAndReps;

    /// <summary>Total number of sets including warm-ups.</summary>
    public int TotalSetCount => SetCount + WarmupSetCount;
}

