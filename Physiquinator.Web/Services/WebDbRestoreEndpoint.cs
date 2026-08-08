using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.Json;

namespace Physiquinator.Web.Services;

/// <summary>
/// Receives the browser's IndexedDB copies of the SQLite databases before the Blazor
/// circuit starts, so a freshly restarted dyno opens with the visitor's data intact.
/// </summary>
public static class WebDbRestoreEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointConventionBuilder MapPhysiquinatorBrowserDbRestore(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/api/db/restore")
    {
        return endpoints.MapPost(pattern, async (HttpContext context) =>
        {
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

                    // The old WAL may reference frames from a different database file; discard it.
                    foreach (var sidecar in new[] { finalPath + "-wal", finalPath + "-shm" }.Where(File.Exists))
                    {
                        File.Delete(sidecar);
                    }

                    restored++;
                }
                catch (FormatException)
                {
                    // Invalid base64 in one file; keep restoring the others.
                }
                catch (IOException ex)
                {
                    // Another circuit may hold the file open; it will retry on the next page load.
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
