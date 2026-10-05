namespace Physiquinator.Core;

public sealed record HistoryCrumb(string Title, string? Href = null)
{
    public static HistoryCrumb History(string? historyQuery = null) =>
        new("History", AppRoutes.HistoryWithQuery(historyQuery));

    public static HistoryCrumb Current(string title) => new(title);
}
