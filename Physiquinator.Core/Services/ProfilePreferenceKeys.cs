namespace Physiquinator.Core.Services;

/// <summary>
/// Canonical rule for preference keys that are stored per user profile.
/// The demo profile (Guid.Empty) owns the bare legacy key. Every other profile
/// gets the key suffixed with "_&lt;profileId&gt;".
/// </summary>
public static class ProfilePreferenceKeys
{
    public static string For(string baseKey, Guid activeProfileId) =>
        activeProfileId == UserProfileService.DemoProfileId ? baseKey : $"{baseKey}_{activeProfileId}";

    public static string For(string baseKey, Models.UserProfile activeProfile) => For(baseKey, activeProfile.Id);

    /// <summary>
    /// The suffix appended to the bare key for a non-demo profile. Empty for the demo profile.
    /// Used by backup/restore to strip and re-apply the profile scope.
    /// </summary>
    public static string GetSuffix(Guid activeProfileId) =>
        activeProfileId == UserProfileService.DemoProfileId ? string.Empty : $"_{activeProfileId}";
}
