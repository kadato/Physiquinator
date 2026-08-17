using System.Text.Json;

namespace Physiquinator.Core.Services.Ai.Tools;

public sealed class GetAppSettingsTool(
    ThemeService themeService,
    RestAlertSettingsService restSettings,
    WorkoutScheduleService scheduleService,
    AppUpdateSettingsService updateSettings) : IAiTool
{
    public string Name => "get_app_settings";
    public string Description => "Get current app configuration (Theme preference, Rest alert timer settings, Workout schedule days, Auto-update checks).";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>()
    };

    public Task<string> ExecuteAsync(string argumentsJson)
    {
        var result = new
        {
            themePreference = themeService.Preference,
            effectiveTheme = themeService.EffectiveTheme,
            restAlertsEnabled = restSettings.Enabled,
            restTimerAddTimeSeconds = restSettings.AddTimeSeconds,
            scheduledWorkoutDays = scheduleService.Days.Select(d => d.ToString()),
            autoUpdateCheckEnabled = updateSettings.AutoCheckEnabled
        };

        return Task.FromResult(JsonSerializer.Serialize(result));
    }
}

public sealed class ChangeAppThemeTool(ThemeService themeService) : IAiTool
{
    public string Name => "change_app_theme";
    public string Description => "Change the application appearance theme ('system', 'light', or 'dark').";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            theme = new { type = "string", description = "Theme preference: 'system', 'light', or 'dark'" }
        },
        required = new[] { "theme" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("theme", out JsonElement themeProp) || string.IsNullOrWhiteSpace(themeProp.GetString()))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Missing theme parameter" });
        }

        var theme = themeProp.GetString()!.ToLowerInvariant();
        if (theme is not ("system" or "light" or "dark"))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Invalid theme value. Expected 'system', 'light', or 'dark'." });
        }


        if (theme == "system")
        {
            await themeService.ResetStoredPreferenceToSystemAsync();
        }
        else
        {
            await themeService.SetPreferenceAsync(theme);
        }

        return JsonSerializer.Serialize(new { success = true, message = $"Theme preference changed to '{theme}'." });
    }
}

public sealed class UpdateRestTimerSettingsTool(RestAlertSettingsService restSettings) : IAiTool
{
    public string Name => "update_rest_timer_settings";
    public string Description => "Toggle rest alert notifications/sounds and configure default rest timer add time in seconds (5-300).";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            enabled = new { type = "boolean", description = "Enable or disable rest alert notifications" },
            addTimeSeconds = new { type = "integer", description = "Seconds added by the + button on rest timer (5-300)" }
        },
        required = Array.Empty<string>()
    };

    public Task<string> ExecuteAsync(string argumentsJson)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("enabled", out JsonElement enabledProp))
        {
            restSettings.SetEnabled(enabledProp.GetBoolean());
        }

        if (root.TryGetProperty("addTimeSeconds", out JsonElement secondsProp) && secondsProp.TryGetInt32(out var secVal))
        {
            restSettings.SetAddTimeSeconds(secVal);
        }

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Updated rest timer settings (Alerts: {restSettings.Enabled}, Add time: {restSettings.AddTimeSeconds}s)."
        }));
    }
}

public sealed class UpdateWorkoutScheduleTool(WorkoutScheduleService scheduleService) : IAiTool
{
    public string Name => "update_workout_schedule";
    public string Description => "Set scheduled weekly training days (e.g. ['Monday', 'Wednesday', 'Friday']).";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            days = new
            {
                type = "array",
                description = "List of day names (e.g. 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday')",
                items = new { type = "string" }
            }
        },
        required = new[] { "days" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("days", out JsonElement daysProp) || daysProp.ValueKind != JsonValueKind.Array)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Invalid days array" });
        }

        var parsedDays = new List<DayOfWeek>();
        foreach (JsonElement dayElem in daysProp.EnumerateArray())
        {
            if (Enum.TryParse<DayOfWeek>(dayElem.GetString(), true, out DayOfWeek day))
            {
                parsedDays.Add(day);
            }
        }

        await scheduleService.SetDaysAsync(parsedDays);
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Updated scheduled workout days to: {string.Join(", ", parsedDays)}"
        });
    }
}
