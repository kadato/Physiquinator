namespace Physiquinator.Tests;

/// <summary>Deterministic clock for unit tests.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;

    public void Advance(TimeSpan delta) => _utcNow += delta;

    public override DateTimeOffset GetUtcNow() => _utcNow;
}
