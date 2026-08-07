using Physiquinator.Core.Formatting;
using Xunit;

namespace Physiquinator.Tests.Formatting;

public class ExerciseWeightFormatterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    public void FormatBodyweightOffset_bodyweight_only(double? offset)
    {
        Assert.Equal("BW", ExerciseWeightFormatter.FormatBodyweightOffset(offset, null));
        Assert.Equal("BW (85 kg)", ExerciseWeightFormatter.FormatBodyweightOffset(offset, 85));
    }

    [Fact]
    public void FormatBodyweightOffset_positive_offset()
    {
        Assert.Equal("BW + 5 kg (90 kg)", ExerciseWeightFormatter.FormatBodyweightOffset(5, 85));
        Assert.Equal("BW + 5 kg", ExerciseWeightFormatter.FormatBodyweightOffset(5, null));
        Assert.Equal("BW + 2.5 kg (87.5 kg)", ExerciseWeightFormatter.FormatBodyweightOffset(2.5, 85));
    }

    [Fact]
    public void FormatBodyweightOffset_negative_offset()
    {
        Assert.Equal("BW - 5 kg (80 kg)", ExerciseWeightFormatter.FormatBodyweightOffset(-5, 85));
        Assert.Equal("BW - 5 kg", ExerciseWeightFormatter.FormatBodyweightOffset(-5, null));
        Assert.Equal("BW - 2.5 kg (82.5 kg)", ExerciseWeightFormatter.FormatBodyweightOffset(-2.5, 85));
    }

    [Fact]
    public void FormatBodyweightOffset_appends_reps_when_requested()
    {
        Assert.Equal("BW × 8 reps", ExerciseWeightFormatter.FormatBodyweightOffset(null, null, 8));
        Assert.Equal("BW (85 kg) × 8 reps", ExerciseWeightFormatter.FormatBodyweightOffset(null, 85, 8));
        Assert.Equal("BW + 5 kg (90 kg) × 8 reps", ExerciseWeightFormatter.FormatBodyweightOffset(5, 85, 8));
        Assert.Equal("BW - 5 kg (80 kg) × 8 reps", ExerciseWeightFormatter.FormatBodyweightOffset(-5, 85, 8));
    }

    [Fact]
    public void FormatKg_uses_invariant_two_decimal_pattern()
    {
        Assert.Equal("85", ExerciseWeightFormatter.FormatKg(85));
        Assert.Equal("2.5", ExerciseWeightFormatter.FormatKg(2.5));
        Assert.Equal("87.5", ExerciseWeightFormatter.FormatKg(87.5));
        Assert.Equal("12.25", ExerciseWeightFormatter.FormatKg(12.25));
    }

    [Fact]
    public void ToDisplay_converts_kilograms_to_display_unit()
    {
        Assert.Equal(100, ExerciseWeightFormatter.ToDisplay(100, WeightUnit.Kilograms));
        Assert.Equal(220.46226218, ExerciseWeightFormatter.ToDisplay(100, WeightUnit.Pounds), 6);
    }

    [Fact]
    public void ToKg_converts_display_unit_back_to_kilograms()
    {
        Assert.Equal(100, ExerciseWeightFormatter.ToKg(100, WeightUnit.Kilograms));
        Assert.Equal(100, ExerciseWeightFormatter.ToKg(220.46226218, WeightUnit.Pounds), 6);
    }

    [Fact]
    public void FormatWeight_uses_unit_pattern()
    {
        Assert.Equal("85", ExerciseWeightFormatter.FormatWeight(85, WeightUnit.Kilograms));
        Assert.Equal("187.4", ExerciseWeightFormatter.FormatWeight(85, WeightUnit.Pounds));
    }

    [Fact]
    public void FormatWeightWithUnit_appends_unit_suffix()
    {
        Assert.Equal("85 kg", ExerciseWeightFormatter.FormatWeightWithUnit(85, WeightUnit.Kilograms));
        Assert.Equal("187.4 lb", ExerciseWeightFormatter.FormatWeightWithUnit(85, WeightUnit.Pounds));
    }

    [Fact]
    public void FormatBodyweightOffset_uses_pounds_when_requested()
    {
        Assert.Equal("BW (187.4 lb)", ExerciseWeightFormatter.FormatBodyweightOffset(null, 85, WeightUnit.Pounds));
        Assert.Equal("BW + 5 kg (90 kg)", ExerciseWeightFormatter.FormatBodyweightOffset(5, 85, WeightUnit.Kilograms));
        Assert.Equal("BW + 5 kg (90 kg) × 8 reps", ExerciseWeightFormatter.FormatBodyweightOffset(5, 85, 8, WeightUnit.Kilograms));
        Assert.Equal("BW + 11 lb (198.4 lb) × 8 reps", ExerciseWeightFormatter.FormatBodyweightOffset(5, 85, 8, WeightUnit.Pounds));
        Assert.Equal("BW - 11 lb (176.4 lb)", ExerciseWeightFormatter.FormatBodyweightOffset(-5, 85, WeightUnit.Pounds));
    }
}
