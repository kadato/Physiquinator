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
        WorkoutHistoryBackup backup = ParseBackup(json);

        var (sessions, sets) = CountBackup(backup);
        await _repository.ImportBackupAsync(backup);
        return (sessions, sets);
    }

    /// <summary>
    /// Counts what a history JSON file contains (sessions, set rows, bodyweight
    /// entries) without importing anything.
    /// </summary>
    public static Task<HistoryImportPreview> PreviewImportAsync(string json)
    {
        WorkoutHistoryBackup backup = ParseBackup(json);
        var (sessions, sets) = CountBackup(backup);
        var bodyweight = backup.BodyweightEntries?.Count(e => e is not null) ?? 0;
        return Task.FromResult(new HistoryImportPreview(sessions, sets, bodyweight));
    }

    private static WorkoutHistoryBackup ParseBackup(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        WorkoutHistoryBackup? backup = JsonSerializer.Deserialize(json, PhysiquinatorJsonContext.Default.WorkoutHistoryBackup) ?? throw new InvalidOperationException("Failed to deserialize workout history from JSON.");
        if (backup.FormatVersion is < 1 or > SupportedFormatVersion)
            throw new InvalidOperationException($"Unsupported history backup format version {backup.FormatVersion} (supported: 1-{SupportedFormatVersion}).");

        backup.Sessions ??= [];
        return backup;
    }

    private static (int Sessions, int Sets) CountBackup(WorkoutHistoryBackup backup)
    {
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

        return (sessionCount, setCount);
    }
}

/// <summary>What a history import file contains, without touching the database.</summary>
public sealed record HistoryImportPreview(int Sessions, int Sets, int BodyweightEntries);
