using Physiquinator.Core.Data;
using Physiquinator.Core.Serialization;
using System.Text.Json;

namespace Physiquinator.Core.Services;

public sealed class WorkoutHistoryService(WorkoutHistoryRepository repository)
{
    public const int SupportedFormatVersion = 1;

    private readonly WorkoutHistoryRepository _repository = repository;
    private static readonly JsonSerializerOptions s_jsonWrite = new(PhysiquinatorJsonContext.Default.Options) { WriteIndented = true };

    public async Task<string> ExportToJsonAsync()
    {
        WorkoutHistoryBackup backup = await _repository.CreateBackupSnapshotAsync();
        return JsonSerializer.Serialize(backup, s_jsonWrite);
    }

    public Task<int> GetSessionCountAsync() => _repository.GetSessionCountAsync();

    /// <returns>Number of sessions and set rows merged into the database.</returns>
    public async Task<(int Sessions, int Sets)> ImportFromJsonAsync(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        WorkoutHistoryBackup? backup = JsonSerializer.Deserialize(json, PhysiquinatorJsonContext.Default.WorkoutHistoryBackup) ?? throw new InvalidOperationException("Failed to deserialize workout history from JSON.");
        if (backup.FormatVersion is < 1 or > SupportedFormatVersion)
            throw new InvalidOperationException($"Unsupported history backup format version {backup.FormatVersion} (supported: 1–{SupportedFormatVersion}).");

        backup.Sessions ??= [];

        var sessionCount = 0;
        var setCount = 0;
        foreach (WorkoutHistoryBackupEntry? entry in backup.Sessions)
        {
            if (entry is null || entry.Session is null || string.IsNullOrWhiteSpace(entry.Session.Id))
                continue;
            sessionCount++;
            List<WorkoutSetLogEntity> sets = entry.Sets ?? [];
            setCount += sets.Count(s => s is not null);
        }

        await _repository.ImportBackupAsync(backup);
        return (sessionCount, setCount);
    }
}
