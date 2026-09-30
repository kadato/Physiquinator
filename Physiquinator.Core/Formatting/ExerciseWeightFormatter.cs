using System.Globalization;
using Physiquinator.Core.Models;

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

    /// <summary>Formats a stored kilogram value with its unit suffix, for example, "85 kg" or "187.4 lb".</summary>
    public static string FormatWeightWithUnit(double kg, WeightUnit unit) =>
        $"{FormatWeight(kg, unit)} {UnitSuffix(unit)}";

    /// <summary>Formats a stored kilogram value with thousands grouping, for example, "12,345.5".</summary>
    public static string FormatWeightGrouped(double kg, WeightUnit unit) =>
        ToDisplay(kg, unit).ToString(unit == WeightUnit.Pounds ? "#,##0.#" : "#,##0.##", s_invariant);

    /// <summary>
    /// Formats a stored kilogram total with grouping and a non-breaking unit
    /// suffix, for example, "12,346 kg" or "27,216 lb". Volumes are aggregates where
    /// decimal precision reads as false accuracy, so they round to whole
    /// units. The non-breaking space keeps the unit glued to the number so it
    /// never wraps onto its own row inside a narrow stat card.
    /// </summary>
    public static string FormatVolumeWithUnit(double kg, WeightUnit unit)
    {
        var display = Math.Round(ToDisplay(kg, unit), MidpointRounding.AwayFromZero);
        return $"{display.ToString("#,##0", s_invariant)}\u00A0{UnitSuffix(unit)}";
    }

    /// <summary>
    /// Offsets smaller than this are treated as bodyweight-only. This keeps
    /// tiny floating-point residues (for example from a kg/lb round-trip)
    /// that would format as "0" from showing as "+0 kg" instead of "BW".
    /// </summary>
    public const double BodyweightOnlyToleranceKg = 0.005;

    /// <summary>True when the offset means bodyweight-only (null or effectively zero).</summary>
    public static bool IsBodyweightOnly(double? offsetKg) =>
        offsetKg is null || Math.Abs(offsetKg.Value) < BodyweightOnlyToleranceKg;

    /// <summary>
    /// Formats a bodyweight-relative offset for a set summary, for example,
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

        if (IsBodyweightOnly(offsetKg))
        {
            return bodyweightKg.HasValue
                ? $"BW ({FormatWeight(bodyweightKg.Value, unit)} {unitSuffix}){suffix}"
                : $"BW{suffix}";
        }

        var offset = offsetKg!.Value;
        if (offset > 0)
        {
            return bodyweightKg.HasValue
                ? $"BW + {FormatWeight(offset, unit)} {unitSuffix} ({FormatWeight(offset + bodyweightKg.Value, unit)} {unitSuffix}){suffix}"
                : $"BW + {FormatWeight(offset, unit)} {unitSuffix}{suffix}";
        }

        var abs = Math.Abs(offset);
        return bodyweightKg.HasValue
            ? $"BW - {FormatWeight(abs, unit)} {unitSuffix} ({FormatWeight(bodyweightKg.Value - abs, unit)} {unitSuffix}){suffix}"
            : $"BW - {FormatWeight(abs, unit)} {unitSuffix}{suffix}";
    }

    /// <summary>
    /// Calculates the effective lifted load for a bodyweight exercise:
    /// (bodyweightKg * (bodyweightPercent / 100)) + offsetKg.
    /// Returns null if neither bodyweight nor offset is provided.
    /// </summary>
    public static double? ComputeEffectiveWeight(double? offsetKg, double? bodyweightKg, double? bodyweightPercent = null)
    {
        if (bodyweightKg is > 0)
        {
            var share = (bodyweightPercent ?? 100) / 100.0;
            return (bodyweightKg.Value * share) + (offsetKg ?? 0);
        }
        return offsetKg;
    }

    public static string FormatEffectiveWeight(double? offsetKg, double? bodyweightKg, double? bodyweightPercent, WeightUnit unit, ExerciseLogType logType)
    {
        if (logType == ExerciseLogType.Duration)
        {
            if (IsBodyweightOnly(offsetKg))
                return "-";
            return FormatBodyweightOffset(offsetKg, null, unit);
        }

        if (logType == ExerciseLogType.BodyweightReps)
        {
            var effective = ComputeEffectiveWeight(offsetKg, bodyweightKg, bodyweightPercent);
            if (effective.HasValue)
                return FormatWeight(effective.Value, unit);

            if (offsetKg.HasValue)
                return FormatWeight(offsetKg.Value, unit);

            return "-";
        }

        return offsetKg is { } w ? FormatWeight(w, unit) : "-";
    }
}
