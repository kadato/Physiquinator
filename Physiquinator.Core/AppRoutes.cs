namespace Physiquinator.Core;

/// <summary>Canonical Blazor route strings and route builders.</summary>
public static class AppRoutes
{
    public const string Home = "/";
    public const string History = "/history";
    public const string Settings = "/settings";
    public const string PlanEditor = "/plan";

    /// <summary>Base-relative route prefix for the active workout page (no leading slash).</summary>
    public const string WorkoutRoutePrefix = "workout/";

    /// <summary>Base-relative route prefix for the plan editor (no leading slash).</summary>
    public const string PlanRoutePrefix = "plan/";

    /// <summary>Base-relative route prefix for history pages (no leading slash).</summary>
    public const string HistoryRoutePrefix = "history/";

    /// <summary>Base-relative path of the new-plan editor route (no leading slash).</summary>
    public const string PlanRoutePath = "plan";

    public static string Workout(Guid planId, bool forceNew = false) =>
        forceNew ? $"/workout/{planId}?forceNew=true" : $"/workout/{planId}";

    public static string Plan(Guid planId) => $"/plan/{planId}";

    public static string HistorySession(string sessionId) =>
        $"/history/{Uri.EscapeDataString(sessionId)}";

    public static string ExerciseProgress(Guid planId, string exerciseName) =>
        $"/history/exercise-progress/{planId}/{Uri.EscapeDataString(exerciseName)}";
}
