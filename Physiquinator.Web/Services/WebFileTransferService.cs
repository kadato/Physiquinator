using Microsoft.Extensions.Logging;
using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>Writes exported JSON to the server temp folder; import is not supported in the browser host.</summary>
public sealed class WebFileTransferService(ILogger<WebFileTransferService> logger) : IFileTransferService
{
    private static readonly string s_exportDir =
        Path.Combine(Path.GetTempPath(), "physiquinator-web", "exports");

    public async Task ExportJsonAsync(string fileName, string json, string shareTitle = "Export Workout Plan")
    {
        Directory.CreateDirectory(s_exportDir);
        var path = Path.Combine(s_exportDir, fileName);
        await File.WriteAllTextAsync(path, json);
        logger.LogInformation("Exported {File} to {Path}", fileName, path);
    }

    public Task<string?> PickJsonAsync(string pickerTitle)
    {
        logger.LogWarning("JSON import is not supported in the browser debug host.");
        return Task.FromResult<string?>(null);
    }
}
