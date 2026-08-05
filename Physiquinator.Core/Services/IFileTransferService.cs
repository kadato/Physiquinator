namespace Physiquinator.Core.Services;

/// <summary>JSON export/import of plans and history backups.</summary>
public interface IFileTransferService
{
    Task ExportJsonAsync(string fileName, string json, string shareTitle = "Export Workout Plan");

    Task<string?> PickJsonAsync(string pickerTitle);
}
