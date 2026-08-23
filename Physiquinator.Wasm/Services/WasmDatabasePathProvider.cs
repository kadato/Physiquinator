using Physiquinator.Core.Data;

namespace Physiquinator.Wasm.Services;

/// <summary>
/// Maps each profile to its own SQLite file inside the wasm virtual filesystem.
/// The files are plain relative paths. WasmDbPersistence syncs their bytes with
/// Cache Storage so they survive reloads.
/// </summary>
public sealed class WasmDatabasePathProvider : IDatabasePathProvider
{
    public string GetDatabasePath(Guid profileId) => $"physiquinator-{profileId}.db3";
}
