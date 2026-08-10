using Physiquinator.Core.Models;
using Xunit;

namespace Physiquinator.Tests.Models;

public class ExerciseCatalogTests
{
    [Fact]
    public void All_NamesAreUniqueCaseInsensitively()
    {
        var names = ExerciseCatalog.All.Select(e => e.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void All_NonEmptyNames()
    {
        Assert.All(ExerciseCatalog.All, e => Assert.False(string.IsNullOrWhiteSpace(e.Name)));
    }

    [Fact]
    public void All_BodyweightPercentsAreSane()
    {
        foreach (ExerciseCatalogEntry entry in ExerciseCatalog.All.Where(e => e.LogType == ExerciseLogType.BodyweightReps))
        {
            Assert.NotNull(entry.BodyweightPercent);
            Assert.InRange(entry.BodyweightPercent.Value, 1, 300);
        }
    }

    [Fact]
    public void All_DurationEntriesHaveSecondsAsDefaultReps()
    {
        foreach (ExerciseCatalogEntry entry in ExerciseCatalog.All.Where(e => e.LogType == ExerciseLogType.Duration))
        {
            Assert.NotNull(entry.DefaultReps);
            Assert.True(entry.DefaultReps > 0);
            Assert.Null(entry.BodyweightPercent);
        }
    }

    [Fact]
    public void All_WeightedEntriesHaveDefaultLoadAndNoPercent()
    {
        foreach (ExerciseCatalogEntry entry in ExerciseCatalog.All.Where(e => e.LogType == ExerciseLogType.WeightAndReps))
        {
            Assert.NotNull(entry.DefaultWeightKg);
            Assert.Null(entry.BodyweightPercent);
        }
    }

    [Fact]
    public void Find_MatchesCaseInsensitively()
    {
        Assert.NotNull(ExerciseCatalog.Find("push-up"));
        Assert.NotNull(ExerciseCatalog.Find("PUSH-UP"));
        Assert.Null(ExerciseCatalog.Find("Does Not Exist"));
        Assert.Null(ExerciseCatalog.Find(""));
    }

    [Fact]
    public void Find_PullUps_IsFullBodyweight()
    {
        ExerciseCatalogEntry? pullUp = ExerciseCatalog.Find("Pull-Ups");
        Assert.NotNull(pullUp);
        Assert.Equal(ExerciseLogType.BodyweightReps, pullUp.LogType);
        Assert.Equal(100, pullUp.BodyweightShare);
    }

    [Fact]
    public void MergeSuggestionNames_CatalogFirst_Deduplicated()
    {
        var merged = ExerciseCatalog.MergeSuggestionNames(["Bench Press", "My Custom Exercise"]);

        Assert.Equal(merged.Count, merged.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // Catalog entry appears before the history duplicate, and appears exactly once.
        Assert.Equal("Push-Up", merged[0]);
        Assert.Contains("My Custom Exercise", merged);
        Assert.Equal(1, merged.Count(n => n.Equals("Bench Press", StringComparison.OrdinalIgnoreCase)));
    }
}
