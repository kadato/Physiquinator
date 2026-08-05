using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using System.Text.Json;

namespace Physiquinator.Core.Services;

public sealed class UserProfileService
{
    public const string ProfilesKey = PreferenceKeys.UserProfiles;
    public const string ActiveProfileIdKey = PreferenceKeys.ActiveProfileId;
    public const string ShowFirstTimeSeedModalKey = PreferenceKeys.ShowFirstTimeSeedModal;

    /// <summary>Legacy profile (Guid.Empty) that owns the default "physiquinator.db3" database.</summary>
    public static readonly Guid DemoProfileId = Guid.Empty;

    private readonly AppDatabase _database;
    private readonly WorkoutSessionService _sessionService;
    private readonly IAppPreferences _preferences;
    private readonly IDatabasePathProvider _dbPathProvider;
    private readonly TimeProvider _time;

    public UserProfileService(
        AppDatabase database,
        WorkoutSessionService sessionService,
        IAppPreferences preferences,
        IDatabasePathProvider dbPathProvider,
        TimeProvider time)
    {
        _database = database;
        _sessionService = sessionService;
        _preferences = preferences;
        _dbPathProvider = dbPathProvider;
        _time = time;
    }

    public List<UserProfile> GetProfiles()
    {
        var json = _preferences.Get(ProfilesKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            // First time initialization: create the default Demo User profile
            var defaultProfile = new UserProfile
            {
                Id = DemoProfileId, // DemoProfileId corresponds to the legacy/default database name "physiquinator.db3"
                Name = "Demo User",
                CreatedAt = _time.GetUtcNow().UtcDateTime
            };
            var list = new List<UserProfile> { defaultProfile };
            SaveProfiles(list);
            return list;
        }

        try
        {
            return JsonSerializer.Deserialize<List<UserProfile>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public UserProfile GetActiveProfile()
    {
        List<UserProfile> profiles = GetProfiles();
        if (profiles.Count == 0)
        {
            // Corrupt or empty profile list: recover with a fresh default profile
            var fallback = new UserProfile
            {
                Id = DemoProfileId,
                Name = "Demo User",
                CreatedAt = _time.GetUtcNow().UtcDateTime
            };
            profiles.Add(fallback);
            SaveProfiles(profiles);
            return fallback;
        }

        var activeIdStr = _preferences.Get(ActiveProfileIdKey, DemoProfileId.ToString());
        Guid activeId = Guid.TryParse(activeIdStr, out Guid g) ? g : DemoProfileId;
        return profiles.FirstOrDefault(p => p.Id == activeId) ?? profiles[0];
    }

    public async Task SwitchProfileAsync(Guid profileId)
    {
        List<UserProfile> profiles = GetProfiles();
        UserProfile? targetProfile = profiles.FirstOrDefault(p => p.Id == profileId);
        if (targetProfile == null) return;

        // 1. End any active workout session so memory state doesn't leak
        _sessionService.EndWorkout();

        // 2. Persist the active profile selection
        _preferences.Set(ActiveProfileIdKey, profileId.ToString());

        // 3. Resolve the database path for the new user profile and hot-swap the connection
        var dbPath = _dbPathProvider.GetDatabasePath(profileId);
        await _database.SwitchDatabaseAsync(dbPath).ConfigureAwait(false);
    }

    public void CreateProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        List<UserProfile> profiles = GetProfiles();
        var newProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };
        profiles.Add(newProfile);
        SaveProfiles(profiles);
    }

    public async Task DeleteProfileAsync(Guid profileId)
    {
        if (profileId == DemoProfileId)
        {
            throw new InvalidOperationException("The default Demo User profile cannot be deleted.");
        }

        List<UserProfile> profiles = GetProfiles();
        UserProfile? profileToDelete = profiles.FirstOrDefault(p => p.Id == profileId);
        if (profileToDelete == null) return;

        // If the profile to delete is active, switch to the default (Demo User) profile first
        UserProfile active = GetActiveProfile();
        if (active.Id == profileId)
        {
            await SwitchProfileAsync(DemoProfileId).ConfigureAwait(false);
        }

        profiles.Remove(profileToDelete);
        SaveProfiles(profiles);

        // Delete the profile's SQLite database file
        var dbPath = _dbPathProvider.GetDatabasePath(profileId);
        if (File.Exists(dbPath))
        {
            try
            {
                File.Delete(dbPath);
            }
            catch
            {
                // Ignore file system errors if the file is locked or already deleted
            }
        }
    }

    public void RenameProfile(Guid profileId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;

        List<UserProfile> profiles = GetProfiles();
        UserProfile? profile = profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return;

        profile.Name = newName.Trim();
        SaveProfiles(profiles);
    }

    public void UpdateBodyweight(Guid profileId, double? bodyweightKg)
    {
        List<UserProfile> profiles = GetProfiles();
        UserProfile? profile = profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return;

        profile.BodyweightKg = bodyweightKg;
        SaveProfiles(profiles);
    }

    private void SaveProfiles(List<UserProfile> profiles)
    {
        var json = JsonSerializer.Serialize(profiles);
        _preferences.Set(ProfilesKey, json);
    }
}
