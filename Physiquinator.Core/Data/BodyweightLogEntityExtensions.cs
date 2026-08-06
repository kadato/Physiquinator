using System.Globalization;

namespace Physiquinator.Core.Data;

/// <summary>Parsing helpers for the yyyy-MM-dd date stored in <see cref="BodyweightLogEntity.Date"/>.</summary>
public static class BodyweightLogEntityExtensions
{
    /// <summary>Parses the row date, or null when it is not a valid yyyy-MM-dd value.</summary>
    public static DateOnly? GetDateOnlyOrNull(this BodyweightLogEntity entity) =>
        DateOnly.TryParseExact(entity.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : null;
}
