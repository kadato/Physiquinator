namespace Physiquinator.Core.Formatting;

/// <summary>
/// Builds chart X-axis labels: every item when the series is short, otherwise
/// a sparse set of evenly spaced labels (first, last and ticks in between).
/// </summary>
public static class SparseChartLabelBuilder
{
    public static string[] BuildLabels<T>(IReadOnlyList<T> items, Func<T, string> format, int maxLabels)
    {
        var count = items.Count;
        if (count == 0)
            return [];

        maxLabels = Math.Clamp(maxLabels, 2, 12);
        var labels = new string[count];

        if (count <= maxLabels)
        {
            for (var i = 0; i < count; i++)
                labels[i] = format(items[i]);
            return labels;
        }

        for (var tick = 0; tick < maxLabels; tick++)
        {
            var index = tick == maxLabels - 1
                ? count - 1
                : (int)Math.Round(tick * (count - 1) / (double)(maxLabels - 1));
            labels[index] = format(items[index]);
        }

        return labels;
    }
}
