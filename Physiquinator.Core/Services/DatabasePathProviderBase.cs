using Physiquinator.Core.Data;

namespace Physiquinator.Core.Services;

/// <summary>
/// Shared file naming for per-profile SQLite databases. Platform providers
/// only supply the directory, so the <c>physiquinator.db3</c> versus
/// <c>physiquinator_{id}.db3</c> rule lives in one place.
/// </summary>
public abstract class DatabasePathProviderBase : IDatabasePathProvider
{
    protected abstract string DatabaseDirectory { get; }

    public virtual string GetDatabasePath(Guid profileId)
    {
        var fileName = GetDatabaseFileName(profileId);
        return string.IsNullOrEmpty(DatabaseDirectory)
            ? fileName
            : Path.Combine(DatabaseDirectory, fileName);
    }

    protected static string GetDatabaseFileName(Guid profileId) =>
        profileId == UserProfileService.DemoProfileId
            ? "physiquinator.db3"
            : $"physiquinator_{profileId}.db3";

    protected static string GetTenantDatabaseFileName(string tenantKey, Guid profileId) =>
        profileId == UserProfileService.DemoProfileId
            ? $"physiquinator_{tenantKey}.db3"
            : $"physiquinator_{tenantKey}_{profileId}.db3";

    protected static string ResolveDatabaseDirectory(string? overrideDir, string fallbackDir)
    {
        var dir = !string.IsNullOrWhiteSpace(overrideDir) ? overrideDir : fallbackDir;
        Directory.CreateDirectory(dir);
        return dir;
    }
}
