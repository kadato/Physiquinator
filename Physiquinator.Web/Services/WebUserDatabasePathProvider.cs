using Physiquinator.Core.Data;
using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>
/// Per-account SQLite files: every authenticated account gets its own database
/// (plus one per app profile inside it), so users never share data. Registered
/// after AddPhysiquinatorServices so it wins over Core's per-scope clone.
/// </summary>
public sealed class WebUserDatabasePathProvider(WebUserContext userContext, ILogger<WebUserDatabasePathProvider> logger) : DatabasePathProviderBase
{
    protected override string DatabaseDirectory => WebDatabasePathProvider.ResolveDatabaseDirectory();

    public override string GetDatabasePath(Guid profileId)
    {
        var tenant = userContext.TenantKey;
        var fileName = GetTenantDatabaseFileName(tenant, profileId);
        var path = Path.Combine(DatabaseDirectory, fileName);
        logger.LogDebug("Database path for profile {ProfileId}: {Path} (tenant {Tenant}, hasUser {HasUser})", profileId, path, tenant, userContext.HasUser);
        return path;
    }
}
