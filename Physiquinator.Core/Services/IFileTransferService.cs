namespace Physiquinator.Core.Services;

/// <summary>JSON export/import of plans and history backups, plus plain-text sharing.</summary>
public interface IFileTransferService
{
    Task ExportJsonAsync(string fileName, string json, string shareTitle = "Export Workout Plan");

    Task ExportTextAsync(string fileName, string text, string shareTitle = "Export");

    Task<string?> PickJsonAsync(string pickerTitle);
}
