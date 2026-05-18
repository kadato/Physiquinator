using Physiquinator.Data;
using System.Text.Json;

namespace Physiquinator.Services;

public class WorkoutHistoryService
{
    public const int SupportedFormatVersion = 1;

    private readonly WorkoutHistoryRepository _repository;
    private static readonly JsonSerializerOptions s_jsonWrite = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions s_jsonRead = new() { PropertyNameCaseInsensitive = true };

    public WorkoutHistoryService(WorkoutHistoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> ExportToJsonAsync()
    {
        var backup = await _repository.CreateBackupSnapshotAsync();
        return JsonSerializer.Serialize(backup, s_jsonWrite);
    }

    public Task<int> GetSessionCountAsync() => _repository.GetSessionCountAsync();

    /// <returns>Number of sessions and set rows merged into the database.</returns>
    public async Task<(int Sessions, int Sets)> ImportFromJsonAsync(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var backup = JsonSerializer.Deserialize<WorkoutHistoryBackup>(json, s_jsonRead);
        if (backup is null)
            throw new InvalidOperationException("Failed to deserialize workout history from JSON.");

        if (backup.FormatVersion < 1 || backup.FormatVersion > SupportedFormatVersion)
            throw new InvalidOperationException($"Unsupported history backup format version {backup.FormatVersion} (supported: 1–{SupportedFormatVersion}).");

        backup.Sessions ??= new List<WorkoutHistoryBackupEntry>();

        var sessionCount = 0;
        var setCount = 0;
        foreach (var entry in backup.Sessions)
        {
            if (entry is null || entry.Session is null || string.IsNullOrWhiteSpace(entry.Session.Id))
                continue;
            sessionCount++;
            var sets = entry.Sets ?? new List<WorkoutSetLogEntity>();
            setCount += sets.Count(s => s is not null);
        }

        await _repository.ImportBackupAsync(backup);
        return (sessionCount, setCount);
    }
}
