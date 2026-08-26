using System.Text.Json;

namespace Physiquinator.Web.Services;

/// <summary>
/// Receives the browser's IndexedDB copies of the SQLite databases before the Blazor
/// circuit starts, so a freshly restarted dyno opens with the visitor's data intact.
/// </summary>
public static class WebDbRestoreEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Flipped once the first interactive Blazor circuit has started in this process. From that
    /// moment the on-disk databases are open (or were opened) by pooled SQLite
    /// connections, and the server's copy is newer than or equal to anything the
    /// browser could push. Restoring past this point replaces the file out from
    /// under those pooled handles and silently orphans every later write, so the
    /// endpoint declines instead. A fresh process resets the flag, which is the
    /// one case the restore exists for: a freshly restarted dyno.
    /// </summary>
    public static bool CircuitsHaveStarted { get; private set; }

    public static void MarkCircuitsStarted() => CircuitsHaveStarted = true;

    public static IEndpointConventionBuilder MapPhysiquinatorBrowserDbRestore(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/api/db/restore")
    {
        return endpoints.MapPost(pattern, async (HttpContext context) =>
        {
            if (CircuitsHaveStarted)
                return Results.Ok(new { restored = 0 });

            BrowserDbRestoreRequest? request;
            try
            {
                request = await JsonSerializer.DeserializeAsync<BrowserDbRestoreRequest>(
                    context.Request.Body, JsonOptions, context.RequestAborted);
            }
            catch (JsonException)
            {
                return Results.BadRequest("Malformed request body.");
            }

            if (request?.Files is null)
                return Results.BadRequest("Missing files payload.");

            var directory = WebDatabasePathProvider.ResolveDatabaseDirectory();
            var restored = 0;

            foreach (BrowserDbFile file in request.Files)
            {
                var name = Path.GetFileName(file.Name);
                if (!IsAllowedDatabaseName(name) || string.IsNullOrEmpty(file.Data))
                    continue;

                try
                {
                    var bytes = Convert.FromBase64String(file.Data);
                    var finalPath = Path.Combine(directory, name);
                    var stagingPath = finalPath + ".restoring";
                    await File.WriteAllBytesAsync(stagingPath, bytes, context.RequestAborted);
                    File.Move(stagingPath, finalPath, overwrite: true);

                    // The old WAL may reference frames from a different database file. Discard it.
                    foreach (var sidecar in new[] { finalPath + "-wal", finalPath + "-shm" }.Where(File.Exists))
                    {
                        File.Delete(sidecar);
                    }

                    restored++;
                }
                catch (FormatException)
                {
                    // Invalid base64 in one file. Keep restoring the others.
                }
                catch (IOException ex)
                {
                    // Another circuit may hold the file open. It will retry on the next page load.
                    return Results.Problem("Could not write database file.", statusCode: 500, title: ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    // File is locked by SQLite (WAL) or by AV. Same retry semantics as IOException.
                    return Results.Problem("Could not write database file.", statusCode: 500, title: ex.Message);
                }
            }

            return Results.Ok(new { restored });
        })
        .RequireRateLimiting("restore");
    }

    private static bool IsAllowedDatabaseName(string name) =>
        name.StartsWith("physiquinator", StringComparison.Ordinal)
        && name.EndsWith(".db3", StringComparison.Ordinal);

    private sealed record BrowserDbFile(string Name, string Data);

    private sealed record BrowserDbRestoreRequest(IReadOnlyList<BrowserDbFile>? Files);
}
