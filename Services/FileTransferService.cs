using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace Physiquinator.Services;

/// <summary>JSON file export (share sheet) and import (file picker) helpers for plans and history backups.</summary>
public sealed class FileTransferService
{
    private static readonly PlatformFileTypes JsonFileTypes = new()
    {
        { DevicePlatform.iOS, ["public.json"] },
        { DevicePlatform.Android, ["application/json"] },
        { DevicePlatform.WinUI, [".json"] },
        { DevicePlatform.MacCatalyst, ["public.json"] }
    };

    /// <summary>Removes characters that are invalid in file names.</summary>
    public static string SafeFileStem(string name) => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));

    /// <summary>Builds a timestamped JSON file name from a stem, e.g. "Push Day" → "Push_Day_20260101_120000.json".</summary>
    public static string JsonFileName(string stem) => $"{stem}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

    /// <summary>Writes JSON to the app cache directory and opens the platform share sheet.</summary>
    public async Task ExportJsonAsync(string fileName, string json, string shareTitle = "Export Workout Plan")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(filePath, json);
        await Share.RequestAsync(new ShareFileRequest
        {
            Title = shareTitle,
            File = new ShareFile(filePath)
        });
    }

    /// <summary>Lets the user pick a .json file and returns its text content, or null when cancelled.</summary>
    public async Task<string?> PickJsonAsync(string pickerTitle)
    {
        var result = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = pickerTitle,
            FileTypes = new FilePickerFileType(JsonFileTypes)
        });

        if (result == null)
            return null;

        using var stream = await result.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private sealed class PlatformFileTypes : Dictionary<DevicePlatform, IEnumerable<string>> { }
}
