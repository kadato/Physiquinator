namespace Physiquinator.Formatting;

/// <summary>Suggested Y-axis bounds for progression charts (~12% headroom above max, ~8% below min).</summary>
public static class ChartAxisScale
{
    public static double SuggestYAxisMax(double maxValue) =>
        maxValue <= 0 ? 10 : Math.Ceiling(maxValue * 1.12 / 10) * 10;

    public static double SuggestYAxisMin(double minValue)
    {
        if (minValue <= 0)
            return 0;
        var padded = minValue * 0.92;
        return Math.Floor(padded / 10) * 10;
    }
}
