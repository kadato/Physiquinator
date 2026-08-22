using System.Text.Json;
using System.Text.Json.Serialization;

namespace Physiquinator.Core.Models;

public sealed class AiBridgePromptOptions
{
    public bool IncludeWorkoutPlans { get; set; } = true;
    public bool IncludeRecentBodyweight { get; set; } = true;
    public bool IncludeHistoryStats { get; set; } = true;
    public bool IncludeExerciseProgression { get; set; } = false;
}

public sealed class AiBridgeActionDto
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; set; }
}

public sealed class AiBridgePayloadDto
{
    [JsonPropertyName("actions")]
    public List<AiBridgeActionDto> Actions { get; set; } = [];
}

public sealed class AiBridgeAction
{
    public string ToolName { get; init; } = string.Empty;
    public string ArgumentsJson { get; init; } = "{}";
    public string Description { get; init; } = string.Empty;
    public bool IsDestructive { get; init; }
    public bool IsValid { get; init; } = true;
    public string? ValidationError { get; init; }
    public string HumanSummary { get; init; } = string.Empty;
}

public sealed class AiBridgeActionExecutionResult
{
    public string ToolName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string RawJsonResult { get; init; } = string.Empty;
}
