using Physiquinator.Core.Data;
using Physiquinator.Core.Services;

namespace Physiquinator.Wasm.Services;

/// <summary>
/// Maps each profile to its own SQLite file inside the wasm virtual filesystem.
/// The files are plain relative paths. WasmDbPersistence syncs their bytes with
/// Cache Storage so they survive reloads.
/// </summary>
public sealed class WasmDatabasePathProvider : DatabasePathProviderBase
{
    protected override string DatabaseDirectory => string.Empty;

    public override string GetDatabasePath(Guid profileId) => $"physiquinator-{profileId}.db3";
}
