using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Physiquinator.Core.Services.Ai;

public sealed partial class AiClipboardBridgeService(
    AiToolRegistry toolRegistry,
    UserProfileService userProfileService,
    WorkoutPlanService planService,
    WorkoutHistoryRepository historyRepo,
    TimeProvider? timeProvider = null)
{
    private const string ToolLogBodyweight = "log_bodyweight";
    private const string ToolLogBodyweightEntry = "log_bodyweight_entry";

    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private static readonly JsonElement EmptyJsonObjectElement = CreateEmptyJsonObject();

    private static JsonElement CreateEmptyJsonObject()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private static readonly HashSet<string> DestructiveTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "delete_workout_plan",
        "delete_bodyweight_entry"
    };

    public async Task<string> GeneratePromptAsync(string userGoal, AiBridgePromptOptions? options = null)
    {
        options ??= new AiBridgePromptOptions();
        UserProfile activeProfile = userProfileService.GetActiveProfile();
        var activeBw = activeProfile.BodyweightKg?.ToString("F1", CultureInfo.InvariantCulture) ?? "not logged";
        var now = _time.GetLocalNow().LocalDateTime;

        var sb = new StringBuilder();
        sb.AppendLine("You are Physiquinator AI, an expert fitness, strength training, and workout assistant.");
        sb.AppendLine("Analyze the user's request and training context, provide helpful advice, and generate structured actions if changes to the app are requested.");
        sb.AppendLine();
        sb.AppendLine("### Current User Profile:");
        sb.AppendLine($"- Profile Name: {activeProfile.Name}");
        sb.AppendLine($"- Current Bodyweight: {activeBw} kg");
        sb.AppendLine($"- Current Date/Time: {now:F}");
        sb.AppendLine();

        if (options.IncludeWorkoutPlans)
        {
            await AppendWorkoutPlansContextAsync(sb);
        }

        if (options.IncludeRecentBodyweight)
        {
            await AppendBodyweightHistoryContextAsync(sb);
        }

        if (options.IncludeHistoryStats)
        {
            await AppendTrainingHistoryStatsContextAsync(sb);
        }

        AppendToolsSchema(sb);
        AppendResponseInstructions(sb);

        sb.AppendLine("### User Request:");
        sb.AppendLine(string.IsNullOrWhiteSpace(userGoal) ? "Please analyze my training and suggest improvements." : userGoal.Trim());

        return sb.ToString();
    }

    private async Task AppendWorkoutPlansContextAsync(StringBuilder sb)
    {
        List<WorkoutPlan> plans = await planService.GetAllPlansAsync();
        sb.AppendLine("### Current Workout Plans:");
        if (plans.Count == 0)
        {
            sb.AppendLine("No workout plans found.");
            sb.AppendLine();
            return;
        }

        foreach (WorkoutPlan plan in plans)
        {
            sb.AppendLine($"- Plan \"{plan.Name}\" (ID: {plan.Id})");
            foreach (ExercisePlan ex in plan.Exercises.OrderBy(e => e.Order))
            {
                var weightStr = ex.DefaultWeightKg.HasValue ? $"{ex.DefaultWeightKg.Value:F1}kg" : "unspecified";
                sb.AppendLine($"    * {ex.Name}: {ex.SetCount} sets x {ex.DefaultReps} reps @ {weightStr} (Rest: {ex.RestIntervalSeconds}s)");
            }
        }
        sb.AppendLine();
    }

    private async Task AppendBodyweightHistoryContextAsync(StringBuilder sb)
    {
        IReadOnlyList<BodyweightLogEntity> logs = await historyRepo.GetBodyweightLogsAsync(7);
        if (logs.Count == 0) return;

        sb.AppendLine("### Recent Bodyweight History:");
        foreach (BodyweightLogEntity log in logs)
        {
            sb.AppendLine($"- {log.Date}: {log.BodyweightKg:F1} kg");
        }
        sb.AppendLine();
    }

    private async Task AppendTrainingHistoryStatsContextAsync(StringBuilder sb)
    {
        try
        {
            var totalSessions = await historyRepo.GetSessionCountAsync();
            IReadOnlyList<WorkoutSessionLogEntity> recent = await historyRepo.GetRecentSessionsAsync(5);
            sb.AppendLine("### Training History Summary:");
            sb.AppendLine($"- Total Workouts Completed: {totalSessions}");
            if (recent.Count > 0)
            {
                sb.AppendLine("- Recent Workouts:");
                foreach (WorkoutSessionLogEntity s in recent)
                {
                    var plan = string.IsNullOrWhiteSpace(s.PlanName) ? "Custom Workout" : s.PlanName;
                    sb.AppendLine($"    * {s.StartedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}: {plan}");
                }
            }
            sb.AppendLine();
        }
        catch
        {
            // Non-critical context
        }
    }

    private void AppendToolsSchema(StringBuilder sb)
    {
        sb.AppendLine("### Available App Action Schemas (IAiTool):");
        sb.AppendLine("You can perform operations in Physiquinator by emitting tool actions corresponding to these schemas:");
        var toolsList = toolRegistry.GetAllTools().Select(t => new
        {
            tool = t.Name,
            description = t.Description,
            parameters = t.ParametersSchema
        });
        sb.AppendLine("```json");
        sb.AppendLine(JsonSerializer.Serialize(toolsList, IndentedJsonOptions));
        sb.AppendLine("```");
        sb.AppendLine();
    }

    private static void AppendResponseInstructions(StringBuilder sb)
    {
        sb.AppendLine("### Response Format Instructions:");
        sb.AppendLine("1. Write your detailed training recommendations, answers, or coaching guidance in standard markdown.");
        sb.AppendLine("2. If creating plans, logging data, or updating settings, append a single ```json code fence at the very end of your response with the following format:");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"actions\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"tool\": \"<tool_name>\",");
        sb.AppendLine("      \"arguments\": { /* parameter key-values */ }");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
    }

    public IReadOnlyList<AiBridgeAction> ParseResponse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return [];
        }

        var jsonSnippet = ExtractJsonSnippet(responseText);
        if (string.IsNullOrWhiteSpace(jsonSnippet))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonSnippet);
            List<AiBridgeActionDto> actionDtos = ExtractActionDtos(doc.RootElement);
            return [.. actionDtos.Select(BuildBridgeAction)];
        }
        catch (JsonException ex)
        {
            return [
                new AiBridgeAction
                {
                    ToolName = "parsing_error",
                    ArgumentsJson = "{}",
                    IsValid = false,
                    ValidationError = $"JSON Parsing Failed: {ex.Message}",
                    HumanSummary = "Could not read that response as JSON."
                }
            ];
        }
    }

    private static List<AiBridgeActionDto> ExtractActionDtos(JsonElement root)
    {
        var actionDtos = new List<AiBridgeActionDto>();
        if (root.ValueKind == JsonValueKind.Object)
        {
            ExtractFromJsonObject(root, actionDtos);
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            ExtractFromJsonArray(root, actionDtos);
        }
        return actionDtos;
    }

    private static void ExtractFromJsonObject(JsonElement obj, List<AiBridgeActionDto> actionDtos)
    {
        if (obj.TryGetProperty("actions", out JsonElement actionsElem) && actionsElem.ValueKind == JsonValueKind.Array)
        {
            ExtractFromJsonArray(actionsElem, actionDtos);
        }
        else if (obj.TryGetProperty("tool", out _) && TryParseActionElement(obj, out AiBridgeActionDto? singleDto) && singleDto != null)
        {
            actionDtos.Add(singleDto);
        }
    }

    private static void ExtractFromJsonArray(JsonElement arr, List<AiBridgeActionDto> actionDtos)
    {
        foreach (JsonElement item in arr.EnumerateArray())
        {
            if (TryParseActionElement(item, out AiBridgeActionDto? dto) && dto != null)
            {
                actionDtos.Add(dto);
            }
        }
    }

    public async Task<List<AiBridgeActionExecutionResult>> ExecuteActionsAsync(IEnumerable<AiBridgeAction> actions)
    {
        var results = new List<AiBridgeActionExecutionResult>();
        foreach (AiBridgeAction action in actions)
        {
            results.Add(await ExecuteSingleActionAsync(action));
        }
        return results;
    }

    private async Task<AiBridgeActionExecutionResult> ExecuteSingleActionAsync(AiBridgeAction action)
    {
        if (!action.IsValid)
        {
            return new AiBridgeActionExecutionResult
            {
                ToolName = action.ToolName,
                Success = false,
                Message = action.ValidationError ?? "Invalid action.",
                RawJsonResult = "{}"
            };
        }

        try
        {
            var rawResult = await toolRegistry.ExecuteToolAsync(action.ToolName, action.ArgumentsJson);
            var (success, message) = ParseToolExecutionOutcome(rawResult);

            return new AiBridgeActionExecutionResult
            {
                ToolName = action.ToolName,
                Success = success,
                Message = message,
                RawJsonResult = rawResult
            };
        }
        catch (Exception ex)
        {
            return new AiBridgeActionExecutionResult
            {
                ToolName = action.ToolName,
                Success = false,
                Message = ex.Message,
                RawJsonResult = "{}"
            };
        }
    }

    private static (bool Success, string Message) ParseToolExecutionOutcome(string rawResult)
    {
        try
        {
            using var resDoc = JsonDocument.Parse(rawResult);
            JsonElement resRoot = resDoc.RootElement;
            if (resRoot.ValueKind != JsonValueKind.Object)
            {
                return (true, "Executed successfully.");
            }

            var success = !(resRoot.TryGetProperty("success", out JsonElement successProp) && successProp.ValueKind == JsonValueKind.False);
            var message = "Executed successfully.";

            if (resRoot.TryGetProperty("error", out JsonElement errorProp))
            {
                message = errorProp.GetString() ?? message;
            }
            else if (resRoot.TryGetProperty("message", out JsonElement msgProp))
            {
                message = msgProp.GetString() ?? message;
            }

            return (success, message);
        }
        catch
        {
            return (true, "Executed successfully.");
        }
    }

    private static bool TryParseActionElement(JsonElement elem, out AiBridgeActionDto? dto)
    {
        dto = null;
        if (elem.ValueKind != JsonValueKind.Object) return false;

        string? toolName = null;
        if (elem.TryGetProperty("tool", out JsonElement toolProp) && toolProp.ValueKind == JsonValueKind.String)
        {
            toolName = toolProp.GetString();
        }
        else if (elem.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind == JsonValueKind.String)
        {
            toolName = nameProp.GetString();
        }

        if (string.IsNullOrWhiteSpace(toolName)) return false;

        JsonElement args = default;
        if (elem.TryGetProperty("arguments", out JsonElement argsProp))
        {
            args = argsProp;
        }
        else if (elem.TryGetProperty("parameters", out JsonElement paramsProp))
        {
            args = paramsProp;
        }

        dto = new AiBridgeActionDto
        {
            Tool = toolName,
            Arguments = args.ValueKind != JsonValueKind.Undefined ? args : EmptyJsonObjectElement
        };
        return true;
    }

    private AiBridgeAction BuildBridgeAction(AiBridgeActionDto dto)
    {
        var resolvedToolName = ResolveToolAlias(dto.Tool);
        var toolFound = toolRegistry.TryGetTool(resolvedToolName, out IAiTool? tool);
        var argsJson = dto.Arguments.ValueKind != JsonValueKind.Undefined ? dto.Arguments.GetRawText() : "{}";
        var isDestructive = DestructiveTools.Contains(resolvedToolName);

        if (!toolFound || tool == null)
        {
            return new AiBridgeAction
            {
                ToolName = dto.Tool,
                ArgumentsJson = argsJson,
                IsValid = false,
                ValidationError = $"Tool '{dto.Tool}' is not recognized by Physiquinator.",
                HumanSummary = $"Unknown tool: {dto.Tool}",
                IsDestructive = isDestructive
            };
        }

        var humanSummary = GenerateHumanSummary(resolvedToolName, dto.Arguments);

        return new AiBridgeAction
        {
            ToolName = resolvedToolName,
            ArgumentsJson = argsJson,
            Description = tool.Description,
            IsValid = true,
            IsDestructive = isDestructive,
            HumanSummary = humanSummary
        };
    }

    private string ResolveToolAlias(string toolName)
    {
        if (toolRegistry.TryGetTool(toolName, out _)) return toolName;

        return toolName.ToLowerInvariant() switch
        {
            ToolLogBodyweight => toolRegistry.TryGetTool(ToolLogBodyweightEntry, out _) ? ToolLogBodyweightEntry : toolName,
            ToolLogBodyweightEntry => toolRegistry.TryGetTool(ToolLogBodyweight, out _) ? ToolLogBodyweight : toolName,
            "get_bodyweight" => toolRegistry.TryGetTool("get_bodyweight_history", out _) ? "get_bodyweight_history" : toolName,
            "get_stats" => toolRegistry.TryGetTool("get_workout_history_stats", out _) ? "get_workout_history_stats" : toolName,
            _ => toolName
        };
    }

    private static string GenerateHumanSummary(string toolName, JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object) return toolName;

        return toolName.ToLowerInvariant() switch
        {
            "create_workout_plan" => FormatCreatePlanSummary(args),
            "update_workout_plan" => FormatUpdatePlanSummary(args),
            "delete_workout_plan" => $"Delete workout plan (ID: {GetPropertyString(args, "planId")})",
            ToolLogBodyweight or ToolLogBodyweightEntry => FormatLogBodyweightSummary(args),
            "delete_bodyweight_entry" => $"Delete bodyweight entry (ID: {GetPropertyString(args, "id")})",
            "change_app_theme" => $"Change app theme to {GetPropertyString(args, "theme")}",
            "update_rest_timer_settings" => "Update rest timer sound and vibration settings",
            "generate_deload_plan" => "Generate deload workout plan",
            "calculate_progressive_overload" => "Calculate progressive overload recommendations",
            _ => toolName
        };
    }

    private static string GetPropertyString(JsonElement args, string propertyName) =>
        args.TryGetProperty(propertyName, out JsonElement prop) ? prop.GetString() ?? "" : "";

    private static string FormatCreatePlanSummary(JsonElement args)
    {
        var planName = args.TryGetProperty("name", out JsonElement pName) ? pName.GetString() : "New Plan";
        var exCount = args.TryGetProperty("exercises", out JsonElement exs) && exs.ValueKind == JsonValueKind.Array ? exs.GetArrayLength() : 0;
        return $"Create workout plan \"{planName}\" ({exCount} exercises)";
    }

    private static string FormatUpdatePlanSummary(JsonElement args)
    {
        var uName = args.TryGetProperty("name", out JsonElement upName) ? upName.GetString() : "Plan";
        var id = GetPropertyString(args, "planId");
        return $"Update workout plan \"{uName}\" (ID: {id})";
    }

    private static string FormatLogBodyweightSummary(JsonElement args)
    {
        var bw = args.TryGetProperty("bodyweightKg", out JsonElement bwProp) || args.TryGetProperty("weightKg", out bwProp) ? bwProp.GetRawText() : "?";
        var date = args.TryGetProperty("date", out JsonElement dtProp) ? dtProp.GetString() : "Today";
        return $"Log bodyweight {bw} kg for {date}";
    }

    private static string ExtractJsonSnippet(string responseText)
    {
        // Try finding ```json ... ``` code fence first
        Match match = JsonFenceRegex().Match(responseText);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // Try generic ``` ... ``` code fence containing json
        Match genericCodeMatch = GenericCodeFenceRegex().Match(responseText);
        if (genericCodeMatch.Success)
        {
            var content = genericCodeMatch.Groups[1].Value.Trim();
            if (content.StartsWith('{') || content.StartsWith('['))
            {
                return content;
            }
        }

        // Try matching a top-level JSON object or array
        var trimmed = responseText.Trim();
        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) || (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
        {
            return trimmed;
        }

        // Find outermost JSON object with "actions"
        var actionsIdx = responseText.IndexOf("\"actions\"", StringComparison.OrdinalIgnoreCase);
        if (actionsIdx >= 0)
        {
            var openBrace = responseText.LastIndexOf('{', actionsIdx);
            var closeBrace = responseText.LastIndexOf('}');
            if (openBrace >= 0 && closeBrace > openBrace)
            {
                return responseText.Substring(openBrace, closeBrace - openBrace + 1);
            }
        }

        return string.Empty;
    }

    [GeneratedRegex(@"```json\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex JsonFenceRegex();

    [GeneratedRegex(@"```\s*([\s\S]*?)\s*```")]
    private static partial Regex GenericCodeFenceRegex();
}

