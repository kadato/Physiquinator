using Physiquinator.Core.Data;
using Physiquinator.Core.Services;

namespace Physiquinator.Services;

/// <summary>
/// Resolves the SQLite database location for each profile. Honors the
/// <c>PHYSIQUINATOR_DB_DIR</c> env var override (used by tests and the
/// screenshot tooling) and otherwise uses the MAUI app data directory.
/// </summary>
public sealed class DatabasePathProvider : DatabasePathProviderBase
{
    private readonly string _appDataDir;

    public DatabasePathProvider()
    {
        var customDbDir = AppEnvironment.DatabaseDirectoryOverride;
        _appDataDir = !string.IsNullOrEmpty(customDbDir) ? customDbDir : FileSystem.AppDataDirectory;
    }

    protected override string DatabaseDirectory => _appDataDir;
}
