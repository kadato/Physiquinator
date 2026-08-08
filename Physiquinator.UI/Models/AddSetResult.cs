namespace Physiquinator.UI.Models;

/// <summary>Result of the add-set dialog: exercise name, reps, and weight in display units.</summary>
public sealed record AddSetResult(string ExerciseName, int? Reps, double? DisplayWeightKg);
