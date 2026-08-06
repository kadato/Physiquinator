using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Xunit;

namespace Physiquinator.Tests.Data;

public class PersonalRecordCalculatorTests
{
    private static ExerciseSetLogRow Row(string session, DateTime at, int? reps = null, double? weight = null) =>
        new(session, at, reps, weight);

    [Fact]
    public void Compute_WeightAndReps_FindsBestWeightRepsAndVolume()
    {
        var t1 = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 1, 8, 10, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            Row("s1", t1, reps: 10, weight: 60),   // volume 600
            Row("s2", t2, reps: 12, weight: 62.5), // volume 750
            Row("s3", t3, reps: 8, weight: 80),    // volume 640
        };

        PersonalRecords records = PersonalRecordCalculator.Compute(rows, ExerciseLogType.WeightAndReps);

        Assert.Equal(3, records.Entries.Count);
        PersonalRecordEntry weight = Assert.Single(records.Entries, e => e.Kind == ExerciseRecordKind.BestWeight);
        Assert.Equal(80, weight.Value);
        Assert.Equal(t3, weight.CompletedAtUtc);
        Assert.Equal("s3", weight.SessionId);

        PersonalRecordEntry reps = Assert.Single(records.Entries, e => e.Kind == ExerciseRecordKind.MostReps);
        Assert.Equal(12, reps.Value);
        Assert.Equal(t2, reps.CompletedAtUtc);

        PersonalRecordEntry volume = Assert.Single(records.Entries, e => e.Kind == ExerciseRecordKind.BestVolume);
        Assert.Equal(750, volume.Value);
        Assert.Equal(t2, volume.CompletedAtUtc);
    }

    [Fact]
    public void Compute_TiesKeepFirstOccurrence()
    {
        var t1 = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 2, 8, 10, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            Row("s1", t1, reps: 10, weight: 70),
            Row("s2", t2, reps: 10, weight: 70),
        };

        PersonalRecords records = PersonalRecordCalculator.Compute(rows, ExerciseLogType.WeightAndReps);

        PersonalRecordEntry weight = Assert.Single(records.Entries, e => e.Kind == ExerciseRecordKind.BestWeight);
        Assert.Equal(t1, weight.CompletedAtUtc);
        Assert.Equal("s1", weight.SessionId);
    }

    [Fact]
    public void Compute_Duration_TracksLongestSetOnly()
    {
        var t1 = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 3, 8, 10, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            Row("s1", t1, reps: 45),
            Row("s2", t2, reps: 90),
        };

        PersonalRecords records = PersonalRecordCalculator.Compute(rows, ExerciseLogType.Duration);

        Assert.Single(records.Entries);
        PersonalRecordEntry longest = Assert.Single(records.Entries, e => e.Kind == ExerciseRecordKind.LongestDuration);
        Assert.Equal(90, longest.Value);
        Assert.Equal(t2, longest.CompletedAtUtc);
    }

    [Fact]
    public void Compute_BodyweightReps_UsesBodyweightForVolume()
    {
        var t1 = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            Row("s1", t1, reps: 10, weight: 5), // volume 10 * (80 + 5) = 850
        };

        PersonalRecords records = PersonalRecordCalculator.Compute(rows, ExerciseLogType.BodyweightReps, bodyweightKg: 80);

        PersonalRecordEntry volume = Assert.Single(records.Entries, e => e.Kind == ExerciseRecordKind.BestVolume);
        Assert.Equal(850, volume.Value);
        PersonalRecordEntry weight = Assert.Single(records.Entries, e => e.Kind == ExerciseRecordKind.BestWeight);
        Assert.Equal(5, weight.Value);
    }

    [Fact]
    public void Compute_EmptyRows_YieldsNoEntries()
    {
        PersonalRecords records = PersonalRecordCalculator.Compute(Array.Empty<ExerciseSetLogRow>(), ExerciseLogType.WeightAndReps);

        Assert.Empty(records.Entries);
    }

    [Fact]
    public void Compute_SkipsRowsWithoutTheRelevantMetric()
    {
        var t1 = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            Row("s1", t1, reps: 10, weight: null), // reps only
        };

        PersonalRecords records = PersonalRecordCalculator.Compute(rows, ExerciseLogType.WeightAndReps);

        Assert.Null(records.Entries.FirstOrDefault(e => e.Kind == ExerciseRecordKind.BestWeight));
        PersonalRecordEntry reps = Assert.Single(records.Entries, e => e.Kind == ExerciseRecordKind.MostReps);
        Assert.Equal(10, reps.Value);
        PersonalRecordEntry volume = Assert.Single(records.Entries, e => e.Kind == ExerciseRecordKind.BestVolume);
        Assert.Equal(10, volume.Value);
    }

    [Fact]
    public void ComputeVolume_CountsSingleLoggedValue()
    {
        Assert.Equal(10, PersonalRecordCalculator.ComputeVolume(reps: 10, weightKg: null, ExerciseLogType.WeightAndReps));
        Assert.Equal(40, PersonalRecordCalculator.ComputeVolume(reps: null, weightKg: 40, ExerciseLogType.WeightAndReps));
        Assert.Equal(0, PersonalRecordCalculator.ComputeVolume(reps: null, weightKg: null, ExerciseLogType.WeightAndReps));
        Assert.Equal(300, PersonalRecordCalculator.ComputeVolume(reps: 10, weightKg: 30, ExerciseLogType.WeightAndReps));
    }
}
