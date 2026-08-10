namespace Physiquinator.Core.Models;

/// <summary>
/// One entry in the built-in exercise catalog. Selecting a catalog entry in
/// the plan editor pre-fills the logging type, the bodyweight share, and
/// sensible default reps or load. Names are matched case-insensitively.
/// </summary>
public sealed record ExerciseCatalogEntry(
    string Name,
    ExerciseLogType LogType,
    double? BodyweightPercent = null,
    int? DefaultReps = null,
    double? DefaultWeightKg = null)
{
    /// <summary>
    /// Share of the user's bodyweight counted toward volume, in percent
    /// (e.g. 65 for push-ups, 100 for pull-ups). Null means full bodyweight.
    /// Only meaningful when <see cref="LogType"/> is <see cref="ExerciseLogType.BodyweightReps"/>.
    /// </summary>
    public double BodyweightShare => BodyweightPercent ?? 100;
}
