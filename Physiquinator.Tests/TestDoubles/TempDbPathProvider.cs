using Physiquinator.Core.Data;

namespace Physiquinator.Tests.TestDoubles;

/// <summary><see cref="IDatabasePathProvider"/> returning the same path for every profile.</summary>
public sealed class TempDbPathProvider(string path) : IDatabasePathProvider
{
    public string GetDatabasePath(Guid profileId) => path;
}
