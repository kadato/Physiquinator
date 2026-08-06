using Physiquinator.Core.Formatting;
using Xunit;

namespace Physiquinator.Tests.Formatting;

public class SparseChartLabelBuilderTests
{
    private static readonly int[] ShortSeries = [10, 20, 30];

    [Fact]
    public void BuildLabels_EmptySeries_ReturnsEmpty()
    {
        Assert.Empty(SparseChartLabelBuilder.BuildLabels<int>(
            [], i => i.ToString(), maxLabels: 6));
    }

    [Fact]
    public void BuildLabels_ShortSeries_LabelsEveryItem()
    {
        var labels = SparseChartLabelBuilder.BuildLabels(
            ShortSeries, i => $"#{i}", maxLabels: 6);

        Assert.Equal(["#10", "#20", "#30"], labels);
    }

    [Fact]
    public void BuildLabels_LongSeries_LabelsFirstLastAndSparseTicks()
    {
        var items = Enumerable.Range(0, 10).ToArray();
        var labels = SparseChartLabelBuilder.BuildLabels(items, i => i.ToString(), maxLabels: 4);

        Assert.Equal(10, labels.Length);
        Assert.Equal("0", labels[0]);
        Assert.Equal("9", labels[9]);
        Assert.Equal(4, labels.Count(l => l != null));
    }

    [Fact]
    public void BuildLabels_LongSeries_TicksAreEvenlySpaced()
    {
        var items = Enumerable.Range(0, 10).ToArray();
        var labels = SparseChartLabelBuilder.BuildLabels(items, i => i.ToString(), maxLabels: 4);

        // Expected tick indices: 0, 3, 6, 9 (last tick snaps to the final item)
        Assert.Equal("0", labels[0]);
        Assert.Equal("3", labels[3]);
        Assert.Equal("6", labels[6]);
        Assert.Equal("9", labels[9]);
    }

    [Fact]
    public void BuildLabels_MaxLabelsBelowTwo_ClampsToTwo()
    {
        var items = Enumerable.Range(0, 5).ToArray();
        var labels = SparseChartLabelBuilder.BuildLabels(items, i => i.ToString(), maxLabels: 1);

        Assert.Equal(2, labels.Count(l => l != null));
        Assert.Equal("0", labels[0]);
        Assert.Equal("4", labels[4]);
    }

    [Fact]
    public void BuildLabels_MaxLabelsAboveTwelve_ClampsToTwelve()
    {
        var items = Enumerable.Range(0, 5).ToArray();
        var labels = SparseChartLabelBuilder.BuildLabels(items, i => i.ToString(), maxLabels: 50);

        Assert.Equal(5, labels.Count(l => l != null));
    }
}
