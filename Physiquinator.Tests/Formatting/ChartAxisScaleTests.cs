using Physiquinator.Core.Formatting;
using Xunit;

namespace Physiquinator.Tests.Formatting;

public class ChartAxisScaleTests
{
    [Fact]
    public void SuggestYAxisMax_RoundsUpToMultipleOfFourTickSteps()
    {
        // 95 * 1.05 = 99.75; step = ceil(99.75/4 rough ladder) -> 25; max = 4*25*ceil(99.75/100) = 100.
        var scale = ChartAxisScale.SuggestYAxis(95);
        Assert.Equal(100, scale.Max);
        Assert.Equal(25, scale.TickStep);
        Assert.Equal(scale.Max, 4 * scale.TickStep * Math.Ceiling(95 * 1.05 / (4 * scale.TickStep)));
    }

    [Fact]
    public void SuggestYAxisMax_TickStepIsAlwaysAPositiveInteger()
    {
        foreach (var value in new[] { 3.2, 17.5, 95, 102.5, 9079.9, 10169 })
        {
            var scale = ChartAxisScale.SuggestYAxis(value);
            Assert.True(scale.TickStep >= 1);
            Assert.Equal(scale.TickStep, Math.Ceiling(scale.TickStep));
            Assert.True(scale.Max > value);
            Assert.Equal(0, (int)scale.Max % (int)(4 * scale.TickStep));
        }
    }

    [Fact]
    public void SuggestYAxisMax_ZeroOrNegativeFallsBackToCleanScale()
    {
        Assert.Equal(10, ChartAxisScale.SuggestYAxisMax(0));
        Assert.Equal(10, ChartAxisScale.SuggestYAxisMax(-5));

        var scale = ChartAxisScale.SuggestYAxis(0);
        Assert.True(scale.Max >= 10);
        Assert.True(scale.TickStep >= 1);
        Assert.Equal(0, (int)scale.Max % (int)(4 * scale.TickStep));
    }

    [Fact]
    public void SuggestYAxisMin_PadsBelowMinAndRoundsDownToTen()
    {
        Assert.Equal(70, ChartAxisScale.SuggestYAxisMin(80));
        Assert.Equal(0, ChartAxisScale.SuggestYAxisMin(0));
        Assert.Equal(0, ChartAxisScale.SuggestYAxisMin(5));
    }

    [Fact]
    public void SuggestYAxisMin_AllowsVisibleBandWhenMinEqualsMax()
    {
        var min = ChartAxisScale.SuggestYAxisMin(50);
        var max = ChartAxisScale.SuggestYAxisMax(50);
        Assert.True(max > min);
    }
}
