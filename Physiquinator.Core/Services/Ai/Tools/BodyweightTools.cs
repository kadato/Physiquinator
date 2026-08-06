using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using System.Globalization;
using System.Text.Json;

namespace Physiquinator.Core.Services.Ai.Tools;

public sealed class GetBodyweightHistoryTool(WorkoutHistoryRepository repository, UserProfileService profileService) : IAiTool
{
    public string Name => "get_bodyweight_history";
    public string Description => "Get bodyweight log history entries and current user active bodyweight setting.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            limit = new { type = "integer", description = "Maximum entries to retrieve (default 30)" }
        },
        required = Array.Empty<string>()
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        var limit = 30;
        if (!string.IsNullOrWhiteSpace(argumentsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(argumentsJson);
                if (doc.RootElement.TryGetProperty("limit", out JsonElement limProp) && limProp.TryGetInt32(out var lVal))
                {
                    limit = lVal;
                }
            }
            catch { /* fallback to default */ }
        }

        UserProfile activeProfile = profileService.GetActiveProfile();
        IReadOnlyList<BodyweightLogEntity> logs = await repository.GetBodyweightLogsAsync(limit);

        var result = new
        {
            userProfileName = activeProfile.Name,
            currentActiveBodyweightKg = activeProfile.BodyweightKg,
            logs = logs.Select(l => new
            {
                date = l.Date,
                bodyweightKg = l.BodyweightKg,
                loggedAtUtc = l.UpdatedAtUtc
            })
        };

        return JsonSerializer.Serialize(result);
    }
}

public sealed class LogBodyweightTool(WorkoutHistoryRepository repository, UserProfileService profileService) : IAiTool
{
    public string Name => "log_bodyweight_entry";
    public string Description => "Record or update bodyweight for a given date (defaults to today if date omitted) and update the active user profile bodyweight.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            bodyweightKg = new { type = "number", description = "Bodyweight in kilograms (e.g. 78.5)" },
            date = new { type = "string", description = "ISO format date 'yyyy-MM-dd' (optional, defaults to today)" }
        },
        required = new[] { "bodyweightKg" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("bodyweightKg", out JsonElement bwProp) || !bwProp.TryGetDouble(out var bwKg))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Invalid bodyweightKg value" });
        }

        var date = DateOnly.FromDateTime(DateTime.Today);
        if (root.TryGetProperty("date", out JsonElement dateProp) &&
            !string.IsNullOrWhiteSpace(dateProp.GetString()) &&
            DateOnly.TryParseExact(dateProp.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsedDate))
        {
            date = parsedDate;
        }

        await repository.UpsertBodyweightLogAsync(date, bwKg);

        UserProfile activeProfile = profileService.GetActiveProfile();
        profileService.UpdateBodyweight(activeProfile.Id, bwKg);

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Successfully logged bodyweight {bwKg} kg for {date:yyyy-MM-dd}."
        });
    }
}

public sealed class DeleteBodyweightTool(WorkoutHistoryRepository repository) : IAiTool
{
    public string Name => "delete_bodyweight_entry";
    public string Description => "Delete a bodyweight log entry for a specific date.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            date = new { type = "string", description = "ISO format date 'yyyy-MM-dd'" }
        },
        required = new[] { "date" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("date", out JsonElement dateProp) || string.IsNullOrWhiteSpace(dateProp.GetString()) ||
            !DateOnly.TryParseExact(dateProp.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Invalid or missing date (expected yyyy-MM-dd)" });
        }

        var deleted = await repository.DeleteBodyweightLogAsync(date);
        return JsonSerializer.Serialize(new
        {
            success = deleted,
            message = deleted ? $"Deleted bodyweight log for {date:yyyy-MM-dd}." : $"No bodyweight record found for {date:yyyy-MM-dd}."
        });
    }
}
