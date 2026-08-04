namespace Physiquinator.Data;

/// <summary>Parsing helpers for <see cref="WorkoutSessionLogEntity"/>.</summary>
public static class WorkoutSessionLogEntityExtensions
{
    /// <summary>Parses <see cref="WorkoutSessionLogEntity.WorkoutPlanId"/> as a <see cref="Guid"/>, or null when unparseable.</summary>
    public static Guid? TryGetPlanId(this WorkoutSessionLogEntity session) =>
        Guid.TryParse(session.WorkoutPlanId, out var g) ? g : null;
}
