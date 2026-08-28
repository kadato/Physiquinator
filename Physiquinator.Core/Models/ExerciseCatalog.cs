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
    /// starting points. Users can adjust the share per exercise in the plan.
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
        new("Bicycle Crunch", ExerciseLogType.BodyweightReps, 30, 20),
        new("V-Up", ExerciseLogType.BodyweightReps, 50, 12),
        new("Hanging Leg Raise", ExerciseLogType.BodyweightReps, 100, 12),
        new("Hanging Knee Raise", ExerciseLogType.BodyweightReps, 90, 15),
        new("Toes-to-Bar", ExerciseLogType.BodyweightReps, 100, 8),
        new("Knees-to-Elbows", ExerciseLogType.BodyweightReps, 95, 10),
        new("Russian Twist", ExerciseLogType.BodyweightReps, 30, 20),
        new("Mountain Climbers", ExerciseLogType.BodyweightReps, 40, 20),
        new("Burpees", ExerciseLogType.BodyweightReps, 100, 10),
        new("Jumping Jacks", ExerciseLogType.BodyweightReps, 30, 30),
        new("High Knees", ExerciseLogType.BodyweightReps, 40, 30),
        new("Box Jump", ExerciseLogType.BodyweightReps, 100, 8),
        new("Broad Jump", ExerciseLogType.BodyweightReps, 100, 6),
        new("Bear Crawl", ExerciseLogType.BodyweightReps, 60, 12),
        new("Clapping Push-Up", ExerciseLogType.BodyweightReps, 70, 8),
        new("One-Arm Push-Up", ExerciseLogType.BodyweightReps, 85, 5),
        new("Archer Pull-Up", ExerciseLogType.BodyweightReps, 100, 5),
        new("Commando Pull-Up", ExerciseLogType.BodyweightReps, 100, 6),
        new("Typewriter Pull-Up", ExerciseLogType.BodyweightReps, 100, 5),
        new("Shrimp Squat", ExerciseLogType.BodyweightReps, 100, 6),
        new("Sissy Squat", ExerciseLogType.BodyweightReps, 70, 12),
        new("Cossack Squat", ExerciseLogType.BodyweightReps, 60, 10),
        new("Curtsy Lunge", ExerciseLogType.BodyweightReps, 55, 10),
        new("Walking Lunge", ExerciseLogType.BodyweightReps, 55, 12),
        new("Single-Leg Glute Bridge", ExerciseLogType.BodyweightReps, 70, 12),
        new("Donkey Kick", ExerciseLogType.BodyweightReps, 40, 15),
        new("Fire Hydrant", ExerciseLogType.BodyweightReps, 40, 15),
        new("Superman", ExerciseLogType.BodyweightReps, 30, 12),

        // ---- Duration (hold) exercises: the reps column stores seconds ----
        new("Plank", ExerciseLogType.Duration, null, 45),
        new("Side Plank", ExerciseLogType.Duration, null, 30),
        new("Wall Sit", ExerciseLogType.Duration, null, 45),
        new("Hollow Body Hold", ExerciseLogType.Duration, null, 30),
        new("Dead Hang", ExerciseLogType.Duration, null, 30),
        new("Active Hang", ExerciseLogType.Duration, null, 30),
        new("Passive Hang", ExerciseLogType.Duration, null, 40),
        new("L-Sit Hold", ExerciseLogType.Duration, null, 20),
        new("Handstand Hold", ExerciseLogType.Duration, null, 30),
        new("Superman Hold", ExerciseLogType.Duration, null, 30),
        new("Boat Hold", ExerciseLogType.Duration, null, 30),
        new("Bridge Hold", ExerciseLogType.Duration, null, 30),
        new("Copenhagen Plank", ExerciseLogType.Duration, null, 20),
        new("Hollow Rock Hold", ExerciseLogType.Duration, null, 25),
        new("Crow Pose Hold", ExerciseLogType.Duration, null, 20),
        new("Arch Hold", ExerciseLogType.Duration, null, 25),

        // ---- Weighted: chest ----
        new("Bench Press", ExerciseLogType.WeightAndReps, null, 8, 60),
        new("Incline Bench Press", ExerciseLogType.WeightAndReps, null, 8, 55),
        new("Decline Bench Press", ExerciseLogType.WeightAndReps, null, 8, 62.5),
        new("Dumbbell Bench Press", ExerciseLogType.WeightAndReps, null, 10, 26),
        new("Incline Dumbbell Press", ExerciseLogType.WeightAndReps, null, 10, 22.5),
        new("Decline Dumbbell Press", ExerciseLogType.WeightAndReps, null, 10, 26),
        new("Close-Grip Bench Press", ExerciseLogType.WeightAndReps, null, 8, 50),
        new("Floor Press", ExerciseLogType.WeightAndReps, null, 8, 55),
        new("Machine Chest Press", ExerciseLogType.WeightAndReps, null, 12, 45),
        new("Chest Fly", ExerciseLogType.WeightAndReps, null, 12, 15),
        new("Incline Chest Fly", ExerciseLogType.WeightAndReps, null, 12, 12.5),
        new("Cable Crossover", ExerciseLogType.WeightAndReps, null, 12, 15),
        new("Cable Fly", ExerciseLogType.WeightAndReps, null, 12, 14),
        new("Pec Deck", ExerciseLogType.WeightAndReps, null, 12, 35),

        // ---- Weighted: shoulders ----
        new("Overhead Press", ExerciseLogType.WeightAndReps, null, 8, 40),
        new("Dumbbell Shoulder Press", ExerciseLogType.WeightAndReps, null, 10, 18),
        new("Arnold Press", ExerciseLogType.WeightAndReps, null, 10, 16),
        new("Machine Shoulder Press", ExerciseLogType.WeightAndReps, null, 12, 40),
        new("Lateral Raises", ExerciseLogType.WeightAndReps, null, 12, 8),
        new("Cable Lateral Raise", ExerciseLogType.WeightAndReps, null, 12, 7),
        new("Front Raises", ExerciseLogType.WeightAndReps, null, 12, 8),
        new("Rear Delt Fly", ExerciseLogType.WeightAndReps, null, 12, 8),
        new("Reverse Fly", ExerciseLogType.WeightAndReps, null, 12, 8),
        new("Upright Row", ExerciseLogType.WeightAndReps, null, 10, 30),
        new("Barbell Shrug", ExerciseLogType.WeightAndReps, null, 12, 80),
        new("Dumbbell Shrug", ExerciseLogType.WeightAndReps, null, 12, 32),

        // ---- Weighted: back ----
        new("Barbell Rows", ExerciseLogType.WeightAndReps, null, 10, 55),
        new("Bent-Over Row", ExerciseLogType.WeightAndReps, null, 10, 55),
        new("Single-Arm Dumbbell Row", ExerciseLogType.WeightAndReps, null, 10, 30),
        new("T-Bar Row", ExerciseLogType.WeightAndReps, null, 10, 50),
        new("Chest-Supported Row", ExerciseLogType.WeightAndReps, null, 10, 40),
        new("Seated Cable Row", ExerciseLogType.WeightAndReps, null, 10, 45),
        new("Lat Pulldown", ExerciseLogType.WeightAndReps, null, 10, 50),
        new("Pullover", ExerciseLogType.WeightAndReps, null, 12, 18),
        new("Rack Pull", ExerciseLogType.WeightAndReps, null, 5, 110),
        new("Good Morning", ExerciseLogType.WeightAndReps, null, 10, 40),

        // ---- Weighted: legs / posterior ----
        new("Deadlift", ExerciseLogType.WeightAndReps, null, 5, 100),
        new("Sumo Deadlift", ExerciseLogType.WeightAndReps, null, 5, 95),
        new("Romanian Deadlift", ExerciseLogType.WeightAndReps, null, 8, 80),
        new("Squats", ExerciseLogType.WeightAndReps, null, 5, 100),
        new("Front Squat", ExerciseLogType.WeightAndReps, null, 8, 60),
        new("Goblet Squat", ExerciseLogType.WeightAndReps, null, 12, 24),
        new("Hack Squat", ExerciseLogType.WeightAndReps, null, 10, 90),
        new("Bulgarian Split Squat (Weighted)", ExerciseLogType.WeightAndReps, null, 10, 20),
        new("Leg Press", ExerciseLogType.WeightAndReps, null, 12, 140),
        new("Leg Curls", ExerciseLogType.WeightAndReps, null, 12, 35),
        new("Seated Leg Curl", ExerciseLogType.WeightAndReps, null, 12, 32),
        new("Lying Leg Curl", ExerciseLogType.WeightAndReps, null, 12, 30),
        new("Leg Extensions", ExerciseLogType.WeightAndReps, null, 12, 40),
        new("Calf Raises", ExerciseLogType.WeightAndReps, null, 15, 50),
        new("Standing Calf Raise", ExerciseLogType.WeightAndReps, null, 15, 45),
        new("Seated Calf Raise", ExerciseLogType.WeightAndReps, null, 15, 35),
        new("Hip Thrust (Barbell)", ExerciseLogType.WeightAndReps, null, 10, 70),
        new("Glute Kickback", ExerciseLogType.WeightAndReps, null, 12, 20),

        // ---- Weighted: arms ----
        new("Bicep Curls", ExerciseLogType.WeightAndReps, null, 12, 14),
        new("Hammer Curls", ExerciseLogType.WeightAndReps, null, 12, 14),
        new("Concentration Curl", ExerciseLogType.WeightAndReps, null, 12, 12),
        new("Preacher Curl", ExerciseLogType.WeightAndReps, null, 10, 25),
        new("Incline Dumbbell Curl", ExerciseLogType.WeightAndReps, null, 10, 12),
        new("Cable Curl", ExerciseLogType.WeightAndReps, null, 12, 20),
        new("Dumbbell Curl", ExerciseLogType.WeightAndReps, null, 12, 12),
        new("Tricep Pushdowns", ExerciseLogType.WeightAndReps, null, 12, 20),
        new("Overhead Tricep Extension", ExerciseLogType.WeightAndReps, null, 10, 16),
        new("Skullcrusher", ExerciseLogType.WeightAndReps, null, 10, 30),
        new("Close-Grip Push-Up (Weighted)", ExerciseLogType.WeightAndReps, null, 10, 20),
        new("Cable Overhead Extension", ExerciseLogType.WeightAndReps, null, 12, 18),

        new("Face Pulls", ExerciseLogType.WeightAndReps, null, 15, 15),
        new("Cable Woodchopper", ExerciseLogType.WeightAndReps, null, 12, 18),
        new("Ab Wheel Rollout", ExerciseLogType.WeightAndReps, null, 10, 0),

        // ---- More weighted popular ----
        new("Chest-Supported T-Bar Row", ExerciseLogType.WeightAndReps, null, 10, 45),
        new("Pendlay Row", ExerciseLogType.WeightAndReps, null, 8, 60),
        new("Zercher Squat", ExerciseLogType.WeightAndReps, null, 6, 70),
        new("Overhead Squat", ExerciseLogType.WeightAndReps, null, 6, 40)
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
    /// Formats a bodyweight share for display, for example, "65%" or "100%".
    /// </summary>
    public static string FormatShare(double? bodyweightPercent) =>
        bodyweightPercent is { } p
            ? p.ToString("0.#", CultureInfo.InvariantCulture) + "%"
            : "100%";
}
