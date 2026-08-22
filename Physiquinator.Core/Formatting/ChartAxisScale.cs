namespace Physiquinator.Core.Formatting;

/// <summary>Suggested Y-axis bounds for progression charts.</summary>
/// <remarks>
/// Maxima are rounded up to a multiple of a 1/2/2.5/3/4/5-decade step so the
/// chart's four interpolated divisions land on round numbers (0 / 2.5k / 5k /
/// 7.5k / 10k, never 3024). Minima round down symmetrically for best-weight
/// charts whose floor sits above zero.
/// </remarks>
public static class ChartAxisScale
{
    private static readonly double[] Steps = [1, 2, 2.5, 3, 4, 5];

    /// <summary>Tick step plus an axis max that is an exact multiple of four steps,
    /// so charts render labels like 0 / 2.5k / 5k instead of 960 / 1280.</summary>
    public readonly record struct Scale(double TickStep, double Max);

    public static Scale SuggestYAxis(double maxValue)
    {
        var target = maxValue <= 0 ? 10 : maxValue * 1.05;
        // Integer steps keep MudBlazor's int-typed YAxisTicks exact.
        var step = Math.Max(1, Math.Ceiling(NiceStepCeiling(target / 4)));
        var max = Math.Ceiling(target / (4 * step)) * 4 * step;
        return new Scale(step, max);
    }

    public static double SuggestYAxisMax(double maxValue)
    {
        if (maxValue <= 0)
            return 10;

        var target = maxValue * 1.05;
        var step = NiceStepCeiling(target / 4);
        return Math.Ceiling(target / (4 * step)) * 4 * step;
    }

    public static double SuggestYAxisMin(double minValue)
    {
        if (minValue <= 0)
            return 0;

        var padded = minValue * 0.92;
        return Math.Floor(padded / 10) * 10;
    }

    private static double NiceStepCeiling(double roughStep)
    {
        if (roughStep <= 0)
            return 1;

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(roughStep)));
        foreach (var multiplier in Steps)
        {
            var candidate = multiplier * magnitude;
            if (candidate >= roughStep)
                return candidate;
        }

        return 10 * magnitude;
    }
}

