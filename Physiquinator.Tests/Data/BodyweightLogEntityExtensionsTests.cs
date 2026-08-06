using Physiquinator.Core.Data;
using Xunit;

namespace Physiquinator.Tests.Data;

public class BodyweightLogEntityExtensionsTests
{
    [Fact]
    public void GetDateOnlyOrNull_ParsesIsoDate()
    {
        var entity = new BodyweightLogEntity { Date = "2026-08-06", BodyweightKg = 88.5 };

        Assert.Equal(new DateOnly(2026, 8, 6), entity.GetDateOnlyOrNull());
    }

    [Fact]
    public void GetDateOnlyOrNull_ReturnsNullForInvalidDate()
    {
        var entity = new BodyweightLogEntity { Date = "not-a-date" };

        Assert.Null(entity.GetDateOnlyOrNull());
    }

    [Fact]
    public void GetDateOnlyOrNull_ReturnsNullForEmptyDate()
    {
        var entity = new BodyweightLogEntity { Date = "" };

        Assert.Null(entity.GetDateOnlyOrNull());
    }
}
