using System.Text.Json;
using Microsoft.Maui.Storage;
using Physiquinator.Data;
using Physiquinator.Models;

namespace Physiquinator.Services;

public sealed class UserProfileService
{
    public const string ProfilesKey = "Physiquinator.UserProfiles";
    public const string ActiveProfileIdKey = "Physiquinator.ActiveProfileId";
    public const string ShowFirstTimeSeedModalKey = "Physiquinator.ShowFirstTimeSeedModal";

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
                Id = Guid.Empty, // Guid.Empty corresponds to the legacy/default database name "physiquinator.db3"
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
        var activeIdStr = AppPreferences.Get(ActiveProfileIdKey, Guid.Empty.ToString());
        var activeId = Guid.TryParse(activeIdStr, out var g) ? g : Guid.Empty;
        return profiles.FirstOrDefault(p => p.Id == activeId) ?? profiles.First();
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
        var dbName = profileId == Guid.Empty ? "physiquinator.db3" : $"physiquinator_{profileId}.db3";
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
        if (profileId == Guid.Empty)
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
            await SwitchProfileAsync(Guid.Empty).ConfigureAwait(false);
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

    private void SaveProfiles(List<UserProfile> profiles)
    {
        var json = JsonSerializer.Serialize(profiles);
        AppPreferences.Set(ProfilesKey, json);
    }
}
