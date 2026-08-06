using SQLite;

namespace Physiquinator.Core.Data;

[Table("BodyweightLogs")]
public class BodyweightLogEntity
{
    /// <summary>Local calendar day in yyyy-MM-dd format.</summary>
    [PrimaryKey]
    public string Date { get; set; } = string.Empty;

    public double BodyweightKg { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
