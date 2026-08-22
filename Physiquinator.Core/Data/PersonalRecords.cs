using Physiquinator.Core.Models;

namespace Physiquinator.Core.Data;

/// <summary>One logged set for an exercise (raw metrics, for personal-record computation).</summary>
public sealed record ExerciseSetLogRow(
    string SessionId,
    DateTime CompletedAtUtc,
    int? Reps,
    double? WeightKg);

public enum ExerciseRecordKind
{
    BestWeight = 0,
    MostReps = 1,
    BestVolume = 2,
    LongestDuration = 3
}

/// <summary>A personal record: the value and the instant it was first achieved.</summary>
public sealed record PersonalRecordEntry(
    ExerciseRecordKind Kind,
    double Value,
    DateTime CompletedAtUtc,
    string SessionId);

/// <summary>All personal records for one exercise, ordered by kind.</summary>
public sealed record PersonalRecords(IReadOnlyList<PersonalRecordEntry> Entries);

/// <summary>
/// Computes personal records (PRs) for one exercise from raw set rows.
/// Weight-based records (BestWeight, BestVolume) only apply to weighted log
/// types. Duration exercises track the longest set instead.
/// </summary>
public static class PersonalRecordCalculator
{
    /// <summary>
    /// Rows are consumed in the given order and ties keep the first occurrence,
    /// so callers pass rows chronologically to date a record from its first set.
    /// </summary>
    public static PersonalRecords Compute(
        IEnumerable<ExerciseSetLogRow> rows,
        ExerciseLogType logType,
        double? bodyweightKg = null,
        double? bodyweightPercent = null)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var entries = new List<PersonalRecordEntry>(4);
        if (logType == ExerciseLogType.Duration)
        {
            AppendBest(entries, ExerciseRecordKind.LongestDuration, rows, r => r.Reps is { } reps ? (double?)reps : null);
        }
        else
        {
            AppendBest(entries, ExerciseRecordKind.BestWeight, rows, r => r.WeightKg);
            AppendBest(entries, ExerciseRecordKind.MostReps, rows, r => r.Reps is { } reps ? (double?)reps : null);
            AppendBest(entries, ExerciseRecordKind.BestVolume, rows, r => ComputeVolume(r, logType, bodyweightKg, bodyweightPercent));
        }

        return new PersonalRecords(entries);
    }

    /// <summary>
    /// Volume of a single set: reps × weight. Both metrics must be logged.
    /// A set missing either contributes 0 (no tonnage can be attributed).
    /// Bodyweight-relative exercises include the bodyweight share of the
    /// user's bodyweight when known (100% when no share is set).
    /// </summary>
    public static double ComputeVolume(
        int? reps,
        double? weightKg,
        ExerciseLogType logType,
        double? bodyweightKg = null,
        double? bodyweightPercent = null)
    {
        if (reps is not { } r) return 0;
        if (logType == ExerciseLogType.BodyweightReps && bodyweightKg is > 0)
            return r * ((bodyweightKg.Value * (bodyweightPercent ?? 100) / 100.0) + (weightKg ?? 0));
        if (weightKg is not { } w) return 0;
        return r * w;
    }

    private static double ComputeVolume(ExerciseSetLogRow row, ExerciseLogType logType, double? bodyweightKg, double? bodyweightPercent) =>
        ComputeVolume(row.Reps, row.WeightKg, logType, bodyweightKg, bodyweightPercent);

    private static void AppendBest(
        List<PersonalRecordEntry> entries,
        ExerciseRecordKind kind,
        IEnumerable<ExerciseSetLogRow> rows,
        Func<ExerciseSetLogRow, double?> valueOf)
    {
        PersonalRecordEntry? best = null;
        foreach (ExerciseSetLogRow row in rows)
        {
            if (valueOf(row) is not { } value || value <= 0) continue;
            if (best == null || value > best.Value)
                best = new PersonalRecordEntry(kind, value, row.CompletedAtUtc, row.SessionId);
        }

        if (best != null)
            entries.Add(best);
    }
}
