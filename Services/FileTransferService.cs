using Physiquinator.Core.Services;

namespace Physiquinator.Services;

/// <summary>JSON file export (share sheet) and import (file picker) helpers for plans and history backups.</summary>
public sealed class FileTransferService : IFileTransferService
{
    private static readonly PlatformFileTypes JsonFileTypes = new()
    {
        { DevicePlatform.iOS, ["public.json"] },
        { DevicePlatform.Android, ["application/json"] },
        { DevicePlatform.WinUI, [".json"] },
        { DevicePlatform.MacCatalyst, ["public.json"] }
    };

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

    /// <summary>Writes plain text to the app cache directory and opens the platform share sheet.</summary>
    public async Task ExportTextAsync(string fileName, string text, string shareTitle = "Export")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(filePath, text);
        await Share.RequestAsync(new ShareFileRequest
        {
            Title = shareTitle,
            File = new ShareFile(filePath)
        });
    }

    /// <summary>Writes PNG bytes to the app cache directory and opens the platform share sheet.</summary>
    public async Task ExportImageAsync(string fileName, byte[] pngBytes, string shareTitle = "Share")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(pngBytes);

        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, pngBytes);
        await Share.RequestAsync(new ShareFileRequest
        {
            Title = shareTitle,
            File = new ShareFile(filePath)
        });
    }

    /// <summary>Lets the user pick a .json file and returns its text content, or null when cancelled.</summary>
    public async Task<string?> PickJsonAsync(string pickerTitle)
    {
        FileResult? result = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = pickerTitle,
            FileTypes = new FilePickerFileType(JsonFileTypes)
        });

        if (result == null)
            return null;

        using Stream stream = await result.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private sealed class PlatformFileTypes : Dictionary<DevicePlatform, IEnumerable<string>> { }
}
