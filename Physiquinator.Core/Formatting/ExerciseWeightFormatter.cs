using System.Globalization;

namespace Physiquinator.Formatting;

/// <summary>
/// Weight display strings for set summaries, tables, and charts.
/// All formatting uses invariant culture and a "0.##" pattern.
/// </summary>
public static class ExerciseWeightFormatter
{
    public const string WeightPattern = "0.##";

    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;

    public static string FormatKg(double value) => value.ToString(WeightPattern, s_invariant);

    /// <summary>
    /// Formats a bodyweight-relative offset for a set summary, e.g.
    /// "BW", "BW (85 kg)", "BW + 5 kg (90 kg) × 8 reps", "BW - 5 kg (80 kg) × 8 reps".
    /// </summary>
    /// <param name="offsetKg">Added load relative to bodyweight (0/null means bodyweight only).</param>
    /// <param name="bodyweightKg">User's current bodyweight, when known.</param>
    /// <param name="reps">Optional rep count appended after the weight text.</param>
    public static string FormatBodyweightOffset(double? offsetKg, double? bodyweightKg, int? reps = null)
    {
        var suffix = reps is { } r ? $" × {r} reps" : "";

        if (offsetKg is null or 0)
        {
            return bodyweightKg.HasValue
                ? $"BW ({FormatKg(bodyweightKg.Value)} kg){suffix}"
                : $"BW{suffix}";
        }

        if (offsetKg.Value > 0)
        {
            return bodyweightKg.HasValue
                ? $"BW + {FormatKg(offsetKg.Value)} kg ({FormatKg(offsetKg.Value + bodyweightKg.Value)} kg){suffix}"
                : $"BW + {FormatKg(offsetKg.Value)} kg{suffix}";
        }

        var abs = Math.Abs(offsetKg.Value);
        return bodyweightKg.HasValue
            ? $"BW - {FormatKg(abs)} kg ({FormatKg(bodyweightKg.Value - abs)} kg){suffix}"
            : $"BW - {FormatKg(abs)} kg{suffix}";
    }
}
