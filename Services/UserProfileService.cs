using System.Text.Json;
using Microsoft.Maui.Storage;
using Physiquinator.Data;
using Physiquinator.Models;

namespace Physiquinator.Services;

public sealed class UserProfileService
{
    public const string ProfilesKey = PreferenceKeys.UserProfiles;
    public const string ActiveProfileIdKey = PreferenceKeys.ActiveProfileId;
    public const string ShowFirstTimeSeedModalKey = PreferenceKeys.ShowFirstTimeSeedModal;

    /// <summary>Legacy profile (Guid.Empty) that owns the default "physiquinator.db3" database.</summary>
    public static readonly Guid DemoProfileId = Guid.Empty;

    private readonly AppDatabase _database;
    private readonly WorkoutSessionService _sessionService;

    public UserProfileService(
        AppDatabase database,
        WorkoutSessionService sessionService)
    {
        _database = database;
        _sessionService = sessionService;
    }

    public List<UserProfile> GetProfiles()
    {
        var json = AppPreferences.Get(ProfilesKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            // First time initialization: create the default Demo User profile
            var defaultProfile = new UserProfile
            {
                Id = DemoProfileId, // DemoProfileId corresponds to the legacy/default database name "physiquinator.db3"
                Name = "Demo User",
                CreatedAt = DateTime.UtcNow
            };
            var list = new List<UserProfile> { defaultProfile };
            SaveProfiles(list);
            return list;
        }

        try
        {
            return JsonSerializer.Deserialize<List<UserProfile>>(json) ?? new List<UserProfile>();
        }
        catch
        {
            return new List<UserProfile>();
        }
    }

    public UserProfile GetActiveProfile()
    {
        var profiles = GetProfiles();
        if (profiles.Count == 0)
        {
            // Corrupt or empty profile list: recover with a fresh default profile
            var fallback = new UserProfile
            {
                Id = DemoProfileId,
                Name = "Demo User",
                CreatedAt = DateTime.UtcNow
            };
            profiles.Add(fallback);
            SaveProfiles(profiles);
            return fallback;
        }

        var activeIdStr = AppPreferences.Get(ActiveProfileIdKey, DemoProfileId.ToString());
        var activeId = Guid.TryParse(activeIdStr, out var g) ? g : DemoProfileId;
        return profiles.FirstOrDefault(p => p.Id == activeId) ?? profiles[0];
    }

    public async Task SwitchProfileAsync(Guid profileId)
    {
        var profiles = GetProfiles();
        var targetProfile = profiles.FirstOrDefault(p => p.Id == profileId);
        if (targetProfile == null) return;

        // 1. End any active workout session so memory state doesn't leak
        _sessionService.EndWorkout();

        // 2. Persist the active profile selection
        AppPreferences.Set(ActiveProfileIdKey, profileId.ToString());

        // 3. Resolve the database path for the new user profile
        var dbName = profileId == DemoProfileId ? "physiquinator.db3" : $"physiquinator_{profileId}.db3";
        var customDbDir = Environment.GetEnvironmentVariable("PHYSIQUINATOR_DB_DIR");
        var appDataDir = !string.IsNullOrEmpty(customDbDir) ? customDbDir : FileSystem.AppDataDirectory;
        var dbPath = Path.Combine(appDataDir, dbName);

        // 4. Hot-swap the database connection
        await _database.SwitchDatabaseAsync(dbPath).ConfigureAwait(false);
    }

    public void CreateProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var profiles = GetProfiles();
        var newProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            CreatedAt = DateTime.UtcNow
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

        var profiles = GetProfiles();
        var profileToDelete = profiles.FirstOrDefault(p => p.Id == profileId);
        if (profileToDelete == null) return;

        // If the profile to delete is active, switch to the default (Demo User) profile first
        var active = GetActiveProfile();
        if (active.Id == profileId)
        {
            await SwitchProfileAsync(DemoProfileId).ConfigureAwait(false);
        }

        profiles.Remove(profileToDelete);
        SaveProfiles(profiles);

        // Delete the profile's SQLite database file
        var customDbDir = Environment.GetEnvironmentVariable("PHYSIQUINATOR_DB_DIR");
        var appDataDir = !string.IsNullOrEmpty(customDbDir) ? customDbDir : FileSystem.AppDataDirectory;
        var dbPath = Path.Combine(appDataDir, $"physiquinator_{profileId}.db3");
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

        var profiles = GetProfiles();
        var profile = profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return;

        profile.Name = newName.Trim();
        SaveProfiles(profiles);
    }

    public void UpdateBodyweight(Guid profileId, double? bodyweightKg)
    {
        var profiles = GetProfiles();
        var profile = profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return;

        profile.BodyweightKg = bodyweightKg;
        SaveProfiles(profiles);
    }

    private static void SaveProfiles(List<UserProfile> profiles)
    {
        var json = JsonSerializer.Serialize(profiles);
        AppPreferences.Set(ProfilesKey, json);
    }
}
