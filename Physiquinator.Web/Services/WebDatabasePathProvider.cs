using Physiquinator.Core.Data;
using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>Stores the SQLite database under the temp directory so the browser host never touches real app data.</summary>
public sealed class WebDatabasePathProvider : IDatabasePathProvider
{
    private readonly string _dbDir;

    public WebDatabasePathProvider()
    {
        _dbDir = !string.IsNullOrWhiteSpace(AppEnvironment.DatabaseDirectoryOverride)
            ? AppEnvironment.DatabaseDirectoryOverride
            : Path.Combine(Path.GetTempPath(), "physiquinator-web");
        Directory.CreateDirectory(_dbDir);
    }

    public string GetDatabasePath(Guid profileId)
    {
        var dbName = profileId == UserProfileService.DemoProfileId
            ? "physiquinator.db3"
            : $"physiquinator_{profileId}.db3";
        return Path.Combine(_dbDir, dbName);
    }
}
