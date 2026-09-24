namespace Physiquinator.Core.Models;

/// <summary>
/// Sets the screen wake lock behavior during an active workout.
/// </summary>
public enum WorkoutWakeLockMode
{
    /// <summary>Keep the screen awake only while the rest timer counts down.</summary>
    RestOnly = 0,

    /// <summary>Keep the screen awake for the whole workout.</summary>
    Always = 1,

    /// <summary>Do not request a wake lock. Follow the operating system display sleep timeout.</summary>
    Never = 2
}
