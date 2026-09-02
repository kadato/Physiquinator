using System.Text.Json;

namespace Physiquinator.Core.Services.Ai.Tools;

internal static class AiToolSchemaHelper
{
    public static object EmptySchema() => new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>()
    };

    public static bool TryParsePlanId(JsonElement root, out Guid planId)
    {
        if (root.TryGetProperty("planId", out JsonElement prop) && Guid.TryParse(prop.GetString(), out planId))
        {
            return true;
        }

        planId = Guid.Empty;
        return false;
    }

    public static bool TryParsePlanId(string json, out Guid planId, out string? errorJson, string errorMessage = "Invalid planId")
    {
        planId = Guid.Empty;
        errorJson = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!TryParsePlanId(root, out planId))
            {
                errorJson = JsonSerializer.Serialize(new { success = false, error = errorMessage });
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            errorJson = JsonSerializer.Serialize(new { success = false, error = errorMessage });
            return false;
        }
    }

    public static object PlanIdOnlySchema(string description) => new
    {
        type = "object",
        properties = new
        {
            planId = new { type = "string", description }
        },
        required = new[] { "planId" }
    };

    public static object PlanIdProperty(string description) => new
    {
        type = "string",
        description
    };

    public static object ExerciseItemProperties(bool includeId)
    {
        if (includeId)
        {
            return new
            {
                id = new { type = "string", description = "Existing exercise GUID ID (optional, new GUID generated if omitted)" },
                name = new { type = "string", description = "Exercise name" },
                targetSets = new { type = "integer", description = "Number of sets" },
                targetReps = new { type = "integer", description = "Target reps" },
                targetWeightKg = new { type = "number", description = "Target weight in kg" },
                restTimerSeconds = new { type = "integer", description = "Rest timer seconds" }
            };
        }

        return new
        {
            name = new { type = "string", description = "Exercise name (for example, 'Bench Press')" },
            targetSets = new { type = "integer", description = "Number of sets (for example, 3 or 4)" },
            targetReps = new { type = "integer", description = "Target reps per set (for example, 8 or 10)" },
            targetWeightKg = new { type = "number", description = "Target weight in kg (optional, for example, 80.0)" },
            restTimerSeconds = new { type = "integer", description = "Rest timer seconds (optional, for example, 90)" }
        };
    }
}
