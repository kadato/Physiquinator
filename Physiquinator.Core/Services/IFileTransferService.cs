namespace Physiquinator.Core.Services;

/// <summary>JSON export/import of plans and history backups, plus plain-text and image sharing.</summary>
public interface IFileTransferService
{
    Task ExportJsonAsync(string fileName, string json, string shareTitle = "Export Workout Plan");

    /// <summary>Writes PNG bytes (e.g. a shared workout card) and opens the platform share sheet.</summary>
    Task ExportImageAsync(string fileName, byte[] pngBytes, string shareTitle = "Share");

    Task<string?> PickJsonAsync(string pickerTitle);
}
