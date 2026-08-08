using Physiquinator.Core.Data;
using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>
/// Per-account SQLite files: every authenticated account gets its own database
/// (plus one per app profile inside it), so users never share data. Registered
/// after AddPhysiquinatorServices so it wins over Core's per-scope clone.
/// </summary>
public sealed class WebUserDatabasePathProvider(WebUserContext userContext, ILogger<WebUserDatabasePathProvider> logger) : IDatabasePathProvider
{
    public string GetDatabasePath(Guid profileId)
    {
        var tenant = userContext.TenantKey;
        var dbName = profileId == UserProfileService.DemoProfileId
            ? $"physiquinator_{tenant}.db3"
            : $"physiquinator_{tenant}_{profileId}.db3";
        var path = Path.Combine(WebDatabasePathProvider.ResolveDatabaseDirectory(), dbName);
        logger.LogDebug("Database path for profile {ProfileId}: {Path} (tenant {Tenant}, hasUser {HasUser})", profileId, path, tenant, userContext.HasUser);
        return path;
    }
}
