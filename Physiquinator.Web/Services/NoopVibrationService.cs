using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>No-op haptics for the browser debug host.</summary>
public sealed class NoopVibrationService : IVibrationService
{
    public void Vibrate(TimeSpan duration)
    {
    }
}
