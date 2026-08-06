namespace Physiquinator.Core.Models;

/// <summary>Bodyweight logged for a single calendar day.</summary>
public sealed record BodyweightEntry(DateOnly DateOnly, double BodyweightKg);
