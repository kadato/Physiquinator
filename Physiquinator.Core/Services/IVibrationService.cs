namespace Physiquinator.Core.Services;

/// <summary>Haptic feedback for workout events. No-op on hosts without vibration support.</summary>
public interface IVibrationService
{
    void Vibrate(TimeSpan duration);
}
