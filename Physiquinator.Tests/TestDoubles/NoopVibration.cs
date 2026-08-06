using Physiquinator.Core.Services;

namespace Physiquinator.Tests.TestDoubles;

/// <summary>No-op <see cref="IVibrationService"/>.</summary>
public sealed class NoopVibration : IVibrationService
{
    public void Vibrate(TimeSpan duration)
    {
    }
}
