namespace Physiquinator.Data;

/// <summary>
/// Resolves the on-disk location of the per-profile SQLite database file.
/// </summary>
public interface IDatabasePathProvider
{
    /// <summary>Full path to the SQLite database file for the given profile.</summary>
    string GetDatabasePath(Guid profileId);
}
