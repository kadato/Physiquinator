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
}
