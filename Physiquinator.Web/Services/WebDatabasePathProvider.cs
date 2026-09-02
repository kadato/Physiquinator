using Physiquinator.Core.Data;
using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>Stores the SQLite database under the temp directory so the browser host never touches real app data.</summary>
public sealed class WebDatabasePathProvider : DatabasePathProviderBase
{
    public WebDatabasePathProvider()
    {
        DatabaseDirectory = ResolveDatabaseDirectory();
    }

    /// <summary>The directory all web-host databases live in.</summary>
    protected override string DatabaseDirectory { get; }

    public static string ResolveDatabaseDirectory() =>
        ResolveDatabaseDirectory(
            AppEnvironment.DatabaseDirectoryOverride,
            Path.Combine(Path.GetTempPath(), "physiquinator-web"));
}
