using Physiquinator.Formatting;
using Xunit;

namespace Physiquinator.Tests.Formatting;

public class ChartAxisScaleTests
{
    [Fact]
    public void SuggestYAxisMax_AddsHeadroomAndRoundsUpToTen()
    {
        Assert.Equal(110, ChartAxisScale.SuggestYAxisMax(95));
        Assert.Equal(10, ChartAxisScale.SuggestYAxisMax(0));
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
