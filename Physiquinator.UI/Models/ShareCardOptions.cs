namespace Physiquinator.UI.Models;

/// <summary>
/// Options collected by <c>ShareCardOptionsDialog</c> before capturing the workout card.
/// </summary>
public sealed record ShareCardOptions(
    bool IncludeVolume,
    bool IncludeDuration,
    bool IncludeSets,
    string Theme,
    HashSet<string> IncludedExerciseNames);
