using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Physiquinator.Core.Services.Ai;
using System.Diagnostics;
using System.Text.Json;

namespace Physiquinator.Web.Mcp;

/// <summary>
/// Exposes the app's <see cref="IAiTool"/> registry as MCP tools.
/// Tool names, descriptions, and JSON schemas are taken from the registry as-is,
/// so the MCP surface stays in sync with the in-app assistant automatically.
/// Destructive tools require explicit user confirmation via the multi-round-trip
/// input_required pattern when the client supports it.
/// </summary>
public static class PhysiquinatorMcpTools
{
    private const string ConfirmationRequestKey = "physiquinator-confirm";
    private const string ConfirmProperty = "confirm";

    private static readonly HashSet<string> DestructiveToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "delete_workout_plan",
        "delete_bodyweight_entry"
    };

    public static async ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> context,
        CancellationToken _)
    {
        AiToolRegistry registry = context.Services!.GetRequiredService<AiToolRegistry>();
        var tools = registry.GetAllTools().Select(CreateTool).ToList();

        return new ListToolsResult
        {
            Tools = tools,
            CacheScope = CacheScope.Public,
            TimeToLive = TimeSpan.FromMinutes(5)
        };
    }

    public static async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> context,
        CancellationToken _)
    {
        AiToolRegistry registry = context.Services!.GetRequiredService<AiToolRegistry>();
        var toolName = context.Params.Name;

        if (!registry.TryGetTool(toolName, out IAiTool? tool))
        {
            return ErrorResult($"Tool '{toolName}' is not available.");
        }

        if (IsDestructive(toolName))
        {
            if (context.Server.IsMrtrSupported)
            {
                ConfirmationState confirmationState = GetConfirmation(context.Params);

                if (confirmationState == ConfirmationState.Required)
                {
                    throw CreateConfirmationRequiredException(toolName);
                }

                if (confirmationState == ConfirmationState.Declined)
                {
                    return CancelledResult($"User did not confirm '{toolName}'. No changes were made.");
                }
            }
            else
            {
                context.Server.Services!
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Physiquinator.Mcp")
                    .LogWarning(
                        "Destructive tool {Tool} called by a client without input_required support.",
                        toolName);
            }
        }

        return await ExecuteToolAsync(tool!, context);
    }

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> CallToolTelemetryFilter(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return async (context, cancellationToken) =>
        {
            var stopwatch = Stopwatch.StartNew();
            CallToolResult result = await next(context, cancellationToken);
            stopwatch.Stop();

            ILogger logger = context.Server.Services!
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Physiquinator.Mcp");
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var status = result.IsError == true ? "error" : "success";
            var toolName = context.Params.Name;

            logger.LogInformation(
                "MCP tool {Tool} completed in {ElapsedMs}ms with {Status}.",
                toolName,
                elapsedMs,
                status);

            return result;
        };
    }

    public static Tool CreateTool(IAiTool tool)
    {
        return new Tool
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = SerializeSchema(tool.ParametersSchema),
            Annotations = new ToolAnnotations
            {
                DestructiveHint = IsDestructive(tool.Name) ? true : null,
                ReadOnlyHint = IsReadOnly(tool.Name) ? true : null
            }
        };
    }

    private static async ValueTask<CallToolResult> ExecuteToolAsync(
        IAiTool tool,
        RequestContext<CallToolRequestParams> context)
    {
        var argumentsJson = context.Params.Arguments is null
            ? "{}"
            : JsonSerializer.Serialize(context.Params.Arguments);

        string resultJson;
        try
        {
            resultJson = await tool.ExecuteAsync(argumentsJson);
        }
        catch (Exception ex)
        {
            return ErrorResult($"Tool '{tool.Name}' failed: {ex.Message}");
        }

        using var resultDoc = JsonDocument.Parse(resultJson);
        JsonElement root = resultDoc.RootElement;

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("success", out JsonElement successProp) &&
            successProp.ValueKind == JsonValueKind.False)
        {
            var errorMessage = root.TryGetProperty("error", out JsonElement errorProp) && errorProp.ValueKind == JsonValueKind.String
                ? errorProp.GetString() ?? $"Tool '{tool.Name}' failed."
                : $"Tool '{tool.Name}' failed.";

            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = errorMessage }]
            };
        }

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = resultJson }],
            StructuredContent = root.Clone()
        };
    }

    private static ConfirmationState GetConfirmation(CallToolRequestParams request)
    {
        if (request.InputResponses is null ||
            !request.InputResponses.TryGetValue(ConfirmationRequestKey, out InputResponse? response))
        {
            return ConfirmationState.Required;
        }

        ElicitResult? elicitResult = response.RawValue.Deserialize(InputResponse.ElicitResultJsonTypeInfo);
        if (elicitResult?.IsAccepted != true || elicitResult.Content is null)
        {
            return ConfirmationState.Declined;
        }

        if (elicitResult.Content.TryGetValue(ConfirmProperty, out JsonElement confirmElement) &&
            confirmElement.ValueKind == JsonValueKind.True)
        {
            return ConfirmationState.Confirmed;
        }

        return ConfirmationState.Declined;
    }

    private static InputRequiredException CreateConfirmationRequiredException(string toolName)
    {
        var requests = new Dictionary<string, InputRequest>
        {
            [ConfirmationRequestKey] = InputRequest.ForElicitation(new ElicitRequestParams
            {
                Message = $"The tool '{toolName}' permanently modifies data in Physiquinator. Confirm that you want to continue.",
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                    {
                        [ConfirmProperty] = new ElicitRequestParams.BooleanSchema
                        {
                            Description = "Confirm the destructive action",
                            Default = false
                        }
                    }
                }
            })
        };

        return new InputRequiredException(requests, $"User confirmation is required for destructive tool '{toolName}'.");
    }

    private static JsonElement SerializeSchema(object schema)
    {
        JsonElement element = JsonSerializer.SerializeToElement(schema);
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("type", out JsonElement typeProp) &&
            typeProp.ValueKind == JsonValueKind.String &&
            typeProp.GetString() == "object")
        {
            return element;
        }

        return JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new Dictionary<string, object>(),
            required = Array.Empty<string>()
        });
    }

    private static bool IsDestructive(string toolName) => DestructiveToolNames.Contains(toolName);

    private static bool IsReadOnly(string toolName) => toolName.StartsWith("get_", StringComparison.OrdinalIgnoreCase);

    private static CallToolResult ErrorResult(string message)
    {
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = message }]
        };
    }

    private static CallToolResult CancelledResult(string message)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = message }]
        };
    }

    private enum ConfirmationState
    {
        Required,
        Declined,
        Confirmed
    }
}
