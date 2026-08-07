using Physiquinator.Core.Data;
using Physiquinator.Core.Services;

namespace Physiquinator.Tests.TestDoubles;

/// <summary><see cref="IDatabasePathProvider"/> returning profile-isolated database paths.</summary>
public sealed class TempDbPathProvider(string path) : IDatabasePathProvider
{
    public string GetDatabasePath(Guid profileId)
    {
        if (path == ":memory:")
            return ":memory:";

        return profileId == UserProfileService.DemoProfileId
            ? path
            : Path.ChangeExtension(path, $".{profileId:N}.db3");
    }
}
