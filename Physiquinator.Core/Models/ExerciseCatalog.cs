using System.Globalization;

namespace Physiquinator.Core.Models;

/// <summary>
/// Built-in exercise catalog: bodyweight movements (with the share of the
/// user's bodyweight that counts toward volume), weighted lifts, and
/// duration holds. The plan editor's autocomplete suggests these names
/// alongside names from logged history.
/// </summary>
public static class ExerciseCatalog
{
    /// <summary>
    /// Bodyweight-percent values approximate the lifted mass as a share of
    /// bodyweight (push-ups ~65%, dips ~95%, pull-ups 100%). They are
    /// starting points; users can adjust the share per exercise in the plan.
    /// </summary>
    public static IReadOnlyList<ExerciseCatalogEntry> All { get; } =
    [
        // ---- Bodyweight (reps), share of bodyweight counted toward volume ----
        new("Push-Up", ExerciseLogType.BodyweightReps, 65, 12),
        new("Knee Push-Up", ExerciseLogType.BodyweightReps, 45, 12),
        new("Incline Push-Up", ExerciseLogType.BodyweightReps, 50, 12),
        new("Decline Push-Up", ExerciseLogType.BodyweightReps, 75, 10),
        new("Deficit Push-Up", ExerciseLogType.BodyweightReps, 70, 10),
        new("Diamond Push-Up", ExerciseLogType.BodyweightReps, 65, 10),
        new("Wide Push-Up", ExerciseLogType.BodyweightReps, 65, 12),
        new("Pike Push-Up", ExerciseLogType.BodyweightReps, 70, 10),
        new("Archer Push-Up", ExerciseLogType.BodyweightReps, 80, 8),
        new("Handstand Push-Up", ExerciseLogType.BodyweightReps, 95, 6),
        new("Pseudo Planche Push-Up", ExerciseLogType.BodyweightReps, 75, 8),
        new("Dips", ExerciseLogType.BodyweightReps, 95, 10),
        new("Bench Dips", ExerciseLogType.BodyweightReps, 80, 12),
        new("Muscle-Up", ExerciseLogType.BodyweightReps, 100, 5),
        new("Pull-Ups", ExerciseLogType.BodyweightReps, 100, 8),
        new("Chin-Ups", ExerciseLogType.BodyweightReps, 100, 8),
        new("Inverted Row", ExerciseLogType.BodyweightReps, 60, 10),
        new("Bodyweight Squat", ExerciseLogType.BodyweightReps, 100, 15),
        new("Jump Squat", ExerciseLogType.BodyweightReps, 100, 12),
        new("Pistol Squat", ExerciseLogType.BodyweightReps, 100, 6),
        new("Lunges", ExerciseLogType.BodyweightReps, 50, 12),
        new("Reverse Lunge", ExerciseLogType.BodyweightReps, 50, 12),
        new("Bulgarian Split Squat", ExerciseLogType.BodyweightReps, 75, 10),
        new("Step-Up", ExerciseLogType.BodyweightReps, 75, 10),
        new("Glute Bridge", ExerciseLogType.BodyweightReps, 70, 15),
        new("Hip Thrust", ExerciseLogType.BodyweightReps, 100, 12),
        new("Bodyweight Calf Raise", ExerciseLogType.BodyweightReps, 100, 20),
        new("Nordic Curl", ExerciseLogType.BodyweightReps, 100, 6),
        new("Sit-Up", ExerciseLogType.BodyweightReps, 50, 15),
        new("Crunch", ExerciseLogType.BodyweightReps, 30, 15),
        new("Hanging Leg Raise", ExerciseLogType.BodyweightReps, 100, 12),
        new("Hanging Knee Raise", ExerciseLogType.BodyweightReps, 90, 15),
        new("Russian Twist", ExerciseLogType.BodyweightReps, 30, 20),

        // ---- Duration (hold) exercises: the reps column stores seconds ----
        new("Plank", ExerciseLogType.Duration, null, 45),
        new("Side Plank", ExerciseLogType.Duration, null, 30),
        new("Wall Sit", ExerciseLogType.Duration, null, 45),
        new("Hollow Body Hold", ExerciseLogType.Duration, null, 30),
        new("Dead Hang", ExerciseLogType.Duration, null, 30),
        new("L-Sit Hold", ExerciseLogType.Duration, null, 20),
        new("Handstand Hold", ExerciseLogType.Duration, null, 30),
        new("Superman Hold", ExerciseLogType.Duration, null, 30),

        // ---- Weighted exercises ----
        new("Bench Press", ExerciseLogType.WeightAndReps, null, 8, 60),
        new("Overhead Press", ExerciseLogType.WeightAndReps, null, 8, 40),
        new("Incline Dumbbell Press", ExerciseLogType.WeightAndReps, null, 10, 22.5),
        new("Barbell Rows", ExerciseLogType.WeightAndReps, null, 10, 55),
        new("Deadlift", ExerciseLogType.WeightAndReps, null, 5, 100),
        new("Romanian Deadlift", ExerciseLogType.WeightAndReps, null, 8, 80),
        new("Squats", ExerciseLogType.WeightAndReps, null, 5, 100),
        new("Front Squat", ExerciseLogType.WeightAndReps, null, 8, 60),
        new("Leg Press", ExerciseLogType.WeightAndReps, null, 12, 140),
        new("Leg Curls", ExerciseLogType.WeightAndReps, null, 12, 35),
        new("Leg Extensions", ExerciseLogType.WeightAndReps, null, 12, 40),
        new("Calf Raises", ExerciseLogType.WeightAndReps, null, 15, 50),
        new("Lateral Raises", ExerciseLogType.WeightAndReps, null, 12, 8),
        new("Tricep Pushdowns", ExerciseLogType.WeightAndReps, null, 12, 20),
        new("Overhead Tricep Extension", ExerciseLogType.WeightAndReps, null, 10, 16),
        new("Face Pulls", ExerciseLogType.WeightAndReps, null, 15, 15),
        new("Bicep Curls", ExerciseLogType.WeightAndReps, null, 12, 14),
        new("Hammer Curls", ExerciseLogType.WeightAndReps, null, 12, 14),
        new("Lat Pulldown", ExerciseLogType.WeightAndReps, null, 10, 50),
        new("Seated Cable Row", ExerciseLogType.WeightAndReps, null, 10, 45),
        new("Chest Fly", ExerciseLogType.WeightAndReps, null, 12, 15),
        new("Dumbbell Curl", ExerciseLogType.WeightAndReps, null, 12, 12)
    ];

    private static readonly Dictionary<string, ExerciseCatalogEntry> s_byName = InitByName();

    private static Dictionary<string, ExerciseCatalogEntry> InitByName()
    {
        var dict = new Dictionary<string, ExerciseCatalogEntry>(All.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in All)
            dict[entry.Name] = entry;
        return dict;
    }

    /// <summary>Catalog exercise names, pre-built once.</summary>
    public static IReadOnlyList<string> CatalogNames { get; } =
        [.. All.Select(e => e.Name)];

    /// <summary>Finds a catalog entry by name, O(1) dictionary lookup.</summary>
    public static ExerciseCatalogEntry? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        s_byName.TryGetValue(name.Trim(), out var entry);
        return entry;
    }

    /// <summary>
    /// Catalog exercise names merged with the given history names (catalog
    /// first, de-duplicated case-insensitively) for autocomplete suggestions.
    /// </summary>
    public static IReadOnlyList<string> MergeSuggestionNames(IEnumerable<string> historyNames)
    {
        var seen = new HashSet<string>(CatalogNames.Count + 32, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(CatalogNames.Count + 32);
        foreach (var name in CatalogNames.Where(seen.Add))
            result.Add(name);
        foreach (var name in historyNames.Where(n => !string.IsNullOrWhiteSpace(n) && seen.Add(n)))
            result.Add(name);
        return result;
    }

    /// <summary>
    /// Formats a bodyweight share for display, e.g. "65%" or "100%".
    /// </summary>
    public static string FormatShare(double? bodyweightPercent) =>
        bodyweightPercent is { } p
            ? p.ToString("0.#", CultureInfo.InvariantCulture) + "%"
            : "100%";
}
