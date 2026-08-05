using Physiquinator.Core.Services;

namespace Physiquinator.Services;

/// <summary>MAUI haptic feedback implementation.</summary>
public sealed class MauiVibrationService : IVibrationService
{
    public void Vibrate(TimeSpan duration) => Vibration.Default.Vibrate(duration);
}
