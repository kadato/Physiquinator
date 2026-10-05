namespace Physiquinator.Core;

/// <summary>Canonical Blazor route strings and route builders.</summary>
public static class AppRoutes
{
    public const string Home = "/";
    public const string History = "/history";
    public const string Settings = "/settings";
    public const string PlanEditor = "/plan";
    public const string Bodyweight = "/history/bodyweight";
    public const string Ai = "/ai";
    public const string Privacy = "/privacy";

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

    public static string HistorySession(string sessionId, string? historyQuery, string? fromHistoryQuery = null)
    {
        var baseUrl = HistorySession(sessionId);
        var normalized = NormalizeHistoryQuery(historyQuery ?? fromHistoryQuery);
        return string.IsNullOrWhiteSpace(normalized) ? baseUrl : $"{baseUrl}?hq={Uri.EscapeDataString(normalized)}";
    }

    public static string ExerciseProgress(Guid planId, string exerciseName) =>
        $"/history/exercise-progress/{planId}/{Uri.EscapeDataString(exerciseName)}";

    public static string ExerciseProgress(Guid planId, string exerciseName, string? fromSessionId, string? historyQuery)
    {
        var baseUrl = ExerciseProgress(planId, exerciseName);
        var query = BuildQuery(("from", fromSessionId), ("hq", NormalizeHistoryQuery(historyQuery)));
        return string.IsNullOrEmpty(query) ? baseUrl : $"{baseUrl}?{query}";
    }

    public static string ExerciseAcrossPlans(string exerciseName) =>
        $"/history/exercise/{Uri.EscapeDataString(exerciseName)}";

    public static string ExerciseAcrossPlans(string exerciseName, string? fromSessionId, string? historyQuery)
    {
        var baseUrl = ExerciseAcrossPlans(exerciseName);
        var query = BuildQuery(("from", fromSessionId), ("hq", NormalizeHistoryQuery(historyQuery)));
        return string.IsNullOrEmpty(query) ? baseUrl : $"{baseUrl}?{query}";
    }

    public static string Compare(string sessionAId, string sessionBId) =>
        $"/history/compare?a={Uri.EscapeDataString(sessionAId)}&b={Uri.EscapeDataString(sessionBId)}";

    public static string HistoryWithQuery(string? searchText, IEnumerable<DateOnly>? days)
    {
        var query = BuildQuery(
            ("q", string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim()),
            ("days", days is null ? null : SerializeDays(days)));
        return string.IsNullOrEmpty(query) ? History : $"{History}?{query}";
    }

    public static string HistoryWithQuery(string? rawHistoryQuery)
    {
        var normalized = NormalizeHistoryQuery(rawHistoryQuery);
        return string.IsNullOrEmpty(normalized) ? History : $"{History}?{normalized}";
    }

    public static string? SerializeDays(IEnumerable<DateOnly>? days)
    {
        if (days is null) return null;
        var list = days.OrderBy(d => d).Select(d => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).ToList();
        return list.Count == 0 ? null : string.Join(",", list);
    }

    public static IReadOnlyList<DateOnly> ParseDays(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var result = new List<DateOnly>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (DateOnly.TryParseExact(part, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var day))
                result.Add(day);
        }
        return result;
    }

    private static string? NormalizeHistoryQuery(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim().TrimStart('?');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string BuildQuery(params (string Key, string? Value)[] pairs)
    {
        var parts = new List<string>();
        foreach (var (key, value) in pairs)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            parts.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
        }
        return string.Join("&", parts);
    }
}
