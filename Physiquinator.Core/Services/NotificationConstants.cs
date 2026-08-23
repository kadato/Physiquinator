namespace Physiquinator.Core.Services;

/// <summary>
/// Shared notification and rest-timer constants referenced from core and
/// platform code so values stay consistent across surfaces.
/// </summary>
public static class NotificationConstants
{
    /// <summary>Fallback workout name when no plan is loaded, for example cold-start alarm intents.</summary>
    public const string DefaultFallbackPlanName = "Physiquinator";

    /// <summary>Vibration pattern for the scheduled rest-end alert.</summary>
    public static readonly long[] RestEndVibrationPattern = [0, 400, 200, 400];

    /// <summary>Vibration pattern for the immediate rest-complete alert.</summary>
    public static readonly long[] ImmediateRestCompleteVibrationPattern = [0, 500];
}
