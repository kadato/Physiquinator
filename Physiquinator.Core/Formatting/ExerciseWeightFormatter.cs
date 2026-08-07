using System.Globalization;

namespace Physiquinator.Core.Formatting;

/// <summary>
/// Weight display strings for set summaries, tables, and charts.
/// All formatting uses invariant culture and a "0.##" pattern.
/// </summary>
public static class ExerciseWeightFormatter
{
    public const string WeightPattern = "0.##";
    public const string PoundsPattern = "0.#";
    public const double PoundsPerKg = 2.2046226218;

    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;

    public static string FormatKg(double value) => value.ToString(WeightPattern, s_invariant);

    public static string UnitSuffix(WeightUnit unit) => unit == WeightUnit.Pounds ? "lb" : "kg";

    /// <summary>Converts a stored kilogram value to the display unit.</summary>
    public static double ToDisplay(double kg, WeightUnit unit) =>
        unit == WeightUnit.Pounds ? kg * PoundsPerKg : kg;

    /// <summary>Converts a display-unit value back to kilograms for storage.</summary>
    public static double ToKg(double value, WeightUnit unit) =>
        unit == WeightUnit.Pounds ? value / PoundsPerKg : value;

    /// <summary>Formats a stored kilogram value in the display unit, without a unit suffix.</summary>
    public static string FormatWeight(double kg, WeightUnit unit) =>
        ToDisplay(kg, unit).ToString(unit == WeightUnit.Pounds ? PoundsPattern : WeightPattern, s_invariant);

    /// <summary>Formats a stored kilogram value with its unit suffix, e.g. "85 kg" or "187.4 lb".</summary>
    public static string FormatWeightWithUnit(double kg, WeightUnit unit) =>
        $"{FormatWeight(kg, unit)} {UnitSuffix(unit)}";

    /// <summary>
    /// Formats a bodyweight-relative offset for a set summary, e.g.
    /// "BW", "BW (85 kg)", "BW + 5 kg (90 kg) × 8 reps", "BW - 5 kg (80 kg) × 8 reps".
    /// </summary>
    /// <param name="offsetKg">Added load relative to bodyweight (0/null means bodyweight only).</param>
    /// <param name="bodyweightKg">User's current bodyweight, when known.</param>
    /// <param name="reps">Optional rep count appended after the weight text.</param>
    public static string FormatBodyweightOffset(double? offsetKg, double? bodyweightKg, int? reps = null) =>
        FormatBodyweightOffset(offsetKg, bodyweightKg, reps, WeightUnit.Kilograms);

    public static string FormatBodyweightOffset(double? offsetKg, double? bodyweightKg, WeightUnit unit) =>
        FormatBodyweightOffset(offsetKg, bodyweightKg, null, unit);

    public static string FormatBodyweightOffset(double? offsetKg, double? bodyweightKg, int? reps, WeightUnit unit)
    {
        var suffix = reps is { } r ? $" × {r} reps" : "";
        var unitSuffix = UnitSuffix(unit);

        if (offsetKg is null or 0)
        {
            return bodyweightKg.HasValue
                ? $"BW ({FormatWeight(bodyweightKg.Value, unit)} {unitSuffix}){suffix}"
                : $"BW{suffix}";
        }

        if (offsetKg.Value > 0)
        {
            return bodyweightKg.HasValue
                ? $"BW + {FormatWeight(offsetKg.Value, unit)} {unitSuffix} ({FormatWeight(offsetKg.Value + bodyweightKg.Value, unit)} {unitSuffix}){suffix}"
                : $"BW + {FormatWeight(offsetKg.Value, unit)} {unitSuffix}{suffix}";
        }

        var abs = Math.Abs(offsetKg.Value);
        return bodyweightKg.HasValue
            ? $"BW - {FormatWeight(abs, unit)} {unitSuffix} ({FormatWeight(bodyweightKg.Value - abs, unit)} {unitSuffix}){suffix}"
            : $"BW - {FormatWeight(abs, unit)} {unitSuffix}{suffix}";
    }
}
