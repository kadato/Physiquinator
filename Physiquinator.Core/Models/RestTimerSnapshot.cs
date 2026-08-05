namespace Physiquinator.Core.Models;

/// <summary>Persisted rest countdown state that survives process death.</summary>
public sealed class RestTimerSnapshot
{
    public DateTime? EndUtc { get; set; }

    public int ActiveRestDurationSeconds { get; set; }
}
