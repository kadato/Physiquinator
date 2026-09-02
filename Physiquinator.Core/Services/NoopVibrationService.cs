namespace Physiquinator.Core.Services;

/// <summary>Shared no-op haptics for hosts without vibration support.</summary>
public sealed class NoopVibrationService : IVibrationService
{
    public void Vibrate(TimeSpan duration)
    {
    }
}
