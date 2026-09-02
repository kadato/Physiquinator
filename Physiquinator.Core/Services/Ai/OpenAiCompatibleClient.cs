using Microsoft.Extensions.Logging;
using Physiquinator.Core.Models;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Physiquinator.Core.Services.Ai;

public abstract class AiChatResponseBase
{
    public string ReasoningContent { get; set; } = string.Empty;
    public List<AiToolCallInfo> ToolCalls { get; set; } = [];
    public bool IsError { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed class OpenAiCompatibleResponse : AiChatResponseBase
{
    public string AssistantContent { get; set; } = string.Empty;
}

public sealed class StreamingChatChunk : AiChatResponseBase
{
    public string DeltaContent { get; set; } = string.Empty;
}


public sealed class OpenAiCompatibleClient(HttpClient httpClient, ILogger<OpenAiCompatibleClient>? logger = null)
{
    private const string ReasoningContentProperty = "reasoning_content";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<List<string>> GetAvailableModelsAsync(AiProviderSettings settings, CancellationToken cancellationToken = default)
    {
        var modelsUrl = GetModelsUrl(settings.GetEffectiveBaseUrl());
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);

            AddAuthenticationHeader(request, settings);
            AddProviderHeaders(request, settings);

            HttpResponseMessage httpResponse = await httpClient.SendAsync(request, cancellationToken);
            if (!httpResponse.IsSuccessStatusCode)
            {
                logger?.LogWarning("Models request to {ModelsUrl} failed with HTTP {StatusCode}.", modelsUrl, (int)httpResponse.StatusCode);
                return [];
            }

            var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            return ParseModelsJson(json);
        }
        catch (OperationCanceledException ex)
        {
            logger?.LogWarning(ex, "Models request to {ModelsUrl} was cancelled.", modelsUrl);
            return [];
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to load available models from {ModelsUrl}.", modelsUrl);
            return [];
        }
    }

    private static List<string> ParseModelsJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out JsonElement dataElem) || dataElem.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var modelList = new List<string>();
        foreach (JsonElement item in dataElem.EnumerateArray())
        {
            if (item.TryGetProperty("id", out JsonElement idP) && idP.ValueKind == JsonValueKind.String && idP.GetString() is { Length: > 0 } idStr)
            {
                modelList.Add(idStr);
            }
        }
        return modelList;
    }

    public async IAsyncEnumerable<StreamingChatChunk> StreamChatCompletionAsync(
        AiProviderSettings settings,
        List<AiChatMessage> messageHistory,
        object? toolsSchema = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var endpointUrl = GetChatCompletionsUrl(settings.GetEffectiveBaseUrl());
        using HttpRequestMessage request = BuildHttpRequest(endpointUrl, settings, messageHistory, toolsSchema, stream: true);

        HttpResponseMessage? response = null;
        string? connectionError = null;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            connectionError = $"Connection error: {ex.Message}";
        }

        if (connectionError != null)
        {
            yield return new StreamingChatChunk { IsError = true, ErrorMessage = connectionError };
            yield break;
        }

        if (response == null) yield break;

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errStr = await response.Content.ReadAsStringAsync(cancellationToken);
                yield return new StreamingChatChunk
                {
                    IsError = true,
                    ErrorMessage = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {ExtractErrorDetail(errStr)}"
                };
                yield break;
            }

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            await foreach (StreamingChatChunk chunk in ReadStreamChunksAsync(reader, cancellationToken))
            {
                yield return chunk;
            }
        }
    }

    private static async IAsyncEnumerable<StreamingChatChunk> ReadStreamChunksAsync(
        StreamReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && await reader.ReadLineAsync(cancellationToken) is { } rawLine)
        {
            if (!TryExtractDataJson(rawLine, out var dataJson)) continue;
            if (dataJson.Equals("[DONE]", StringComparison.OrdinalIgnoreCase)) break;

            StreamingChatChunk? chunk = TryParseStreamingChunk(dataJson);
            if (chunk != null)
            {
                yield return chunk;
            }
        }
    }

    private static bool TryExtractDataJson(string rawLine, out string dataJson)
    {
        dataJson = string.Empty;
        if (string.IsNullOrWhiteSpace(rawLine)) return false;

        var line = rawLine.Trim();
        if (!line.StartsWith("data:", StringComparison.Ordinal)) return false;

        dataJson = line["data:".Length..].Trim();
        return true;
    }

    private static StreamingChatChunk? TryParseStreamingChunk(string json)
    {
        try
        {
            return ParseStreamingChunk(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task<OpenAiCompatibleResponse> SendChatCompletionAsync(
        AiProviderSettings settings,
        List<AiChatMessage> messageHistory,
        object? toolsSchema = null,
        CancellationToken cancellationToken = default)
    {
        var response = new OpenAiCompatibleResponse();

        try
        {
            var endpointUrl = GetChatCompletionsUrl(settings.GetEffectiveBaseUrl());
            using HttpRequestMessage request = BuildHttpRequest(endpointUrl, settings, messageHistory, toolsSchema, stream: false);

            HttpResponseMessage httpResponse = await httpClient.SendAsync(request, cancellationToken);
            var responseString = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                response.IsError = true;
                response.ErrorMessage = $"HTTP {(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase}): {ExtractErrorDetail(responseString)}";
                return response;
            }

            ParseJsonResponse(responseString, response);
            return response;
        }
        catch (Exception ex)
        {
            response.IsError = true;
            response.ErrorMessage = $"Connection error: {ex.Message}";
            return response;
        }
    }

    private static HttpRequestMessage BuildHttpRequest(
        string endpointUrl,
        AiProviderSettings settings,
        List<AiChatMessage> messageHistory,
        object? toolsSchema,
        bool stream = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
        AddAuthenticationHeader(request, settings);
        AddProviderHeaders(request, settings);

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = settings.GetEffectiveModel(),
            ["messages"] = FormatMessagesPayload(messageHistory),
            ["stream"] = stream
        };

        if (toolsSchema != null)
        {
            requestBody["tools"] = toolsSchema;
            requestBody["tool_choice"] = "auto";
        }

        var jsonContent = JsonSerializer.Serialize(requestBody, s_jsonOptions);
        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        return request;
    }

    private static void AddAuthenticationHeader(HttpRequestMessage request, AiProviderSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey.Trim());
        }
    }

    private static void AddProviderHeaders(HttpRequestMessage request, AiProviderSettings settings)
    {
        if (settings.Provider is AiProviderType.OpenRouter or AiProviderType.OpenCode)
        {
            request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/kadato/Physiquinator");
            request.Headers.TryAddWithoutValidation("X-Title", "Physiquinator Fitness App");
        }
    }

    private static StreamingChatChunk? ParseStreamingChunk(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("choices", out JsonElement choices) || choices.GetArrayLength() == 0)
        {
            return null;
        }

        JsonElement choice = choices[0];
        if (!choice.TryGetProperty("delta", out JsonElement delta))
        {
            return null;
        }

        var chunk = new StreamingChatChunk();
        if (delta.TryGetProperty("content", out JsonElement contentElem) && contentElem.ValueKind == JsonValueKind.String)
        {
            chunk.DeltaContent = contentElem.GetString() ?? string.Empty;
        }

        if (delta.TryGetProperty(ReasoningContentProperty, out JsonElement rcElem) && rcElem.ValueKind == JsonValueKind.String)
        {
            chunk.ReasoningContent = rcElem.GetString() ?? string.Empty;
        }
        else if (delta.TryGetProperty("thinking", out JsonElement thElem) && thElem.ValueKind == JsonValueKind.String)
        {
            chunk.ReasoningContent = thElem.GetString() ?? string.Empty;
        }

        ParseStreamingToolCalls(delta, chunk.ToolCalls);
        return chunk;
    }

    private static void ParseStreamingToolCalls(JsonElement delta, List<AiToolCallInfo> toolCalls) =>
        AppendToolCalls(delta, toolCalls, static tc =>
        {
            var tcId = tc.TryGetProperty("id", out JsonElement idp) ? idp.GetString() ?? string.Empty : string.Empty;
            var fnName = string.Empty;
            var fnArgs = string.Empty;

            if (tc.TryGetProperty("function", out JsonElement fn))
            {
                if (fn.TryGetProperty("name", out JsonElement np))
                {
                    fnName = np.GetString() ?? string.Empty;
                }

                if (fn.TryGetProperty("arguments", out JsonElement ap))
                {
                    fnArgs = ap.GetString() ?? string.Empty;
                }
            }

            return new AiToolCallInfo { Id = tcId, Name = fnName, ArgumentsJson = fnArgs };
        });

    private static void AppendToolCalls(JsonElement container, List<AiToolCallInfo> toolCalls, Func<JsonElement, AiToolCallInfo> factory)
    {
        if (!container.TryGetProperty("tool_calls", out JsonElement toolCallsElem) || toolCallsElem.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement tc in toolCallsElem.EnumerateArray())
        {
            toolCalls.Add(factory(tc));
        }
    }

    private static List<object> FormatMessagesPayload(List<AiChatMessage> messageHistory)
    {
        return [.. messageHistory.Select<AiChatMessage, object>(m =>
        {
            if (m.Role == AiMessageRole.User)
            {
                return new { role = "user", content = m.Content };
            }

            if (m.Role == AiMessageRole.Tool)
            {
                return new
                {
                    role = "tool",
                    tool_call_id = m.ToolCallId,
                    name = m.ToolName,
                    content = m.Content
                };
            }

            var assistantDict = new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = m.Content ?? string.Empty
            };

            if (!string.IsNullOrEmpty(m.ReasoningContent))
            {
                assistantDict[ReasoningContentProperty] = m.ReasoningContent;
            }
            else if (m.ToolCalls != null && m.ToolCalls.Count > 0)
            {
                // OpenCode / DeepSeek-R1 reasoning mode requires reasoning_content on assistant messages in multi-turn tool loops
                assistantDict[ReasoningContentProperty] = "";
            }

            if (m.ToolCalls != null && m.ToolCalls.Count > 0)
            {
                assistantDict["tool_calls"] = m.ToolCalls.Select(tc => new
                {
                    id = tc.Id,
                    type = "function",
                    function = new
                    {
                        name = tc.Name,
                        arguments = tc.ArgumentsJson
                    }
                }).ToList();
            }

            return assistantDict;
        })];
    }

    private static void ParseJsonResponse(string responseString, OpenAiCompatibleResponse response)
    {
        using var doc = JsonDocument.Parse(responseString);
        if (!doc.RootElement.TryGetProperty("choices", out JsonElement choices) || choices.GetArrayLength() == 0)
        {
            response.IsError = true;
            response.ErrorMessage = "Received empty choice response from AI provider.";
            return;
        }

        JsonElement messageElem = choices[0].GetProperty("message");
        if (messageElem.TryGetProperty("content", out JsonElement contentElem) && contentElem.ValueKind == JsonValueKind.String)
        {
            response.AssistantContent = contentElem.GetString() ?? string.Empty;
        }

        if (messageElem.TryGetProperty(ReasoningContentProperty, out JsonElement rcElem) && rcElem.ValueKind == JsonValueKind.String)
        {
            response.ReasoningContent = rcElem.GetString() ?? string.Empty;
        }
        else if (messageElem.TryGetProperty("thinking", out JsonElement thElem) && thElem.ValueKind == JsonValueKind.String)
        {
            response.ReasoningContent = thElem.GetString() ?? string.Empty;
        }

        ParseJsonResponseToolCalls(messageElem, response.ToolCalls);
    }


    private static void ParseJsonResponseToolCalls(JsonElement messageElem, List<AiToolCallInfo> toolCalls) =>
        AppendToolCalls(messageElem, toolCalls, static tc =>
        {
            var tcId = tc.TryGetProperty("id", out JsonElement idp) ? idp.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
            JsonElement fn = tc.GetProperty("function");
            var fnName = fn.GetProperty("name").GetString() ?? string.Empty;
            var fnArgs = fn.GetProperty("arguments").GetString() ?? "{}";

            return new AiToolCallInfo
            {
                Id = tcId,
                Name = fnName,
                ArgumentsJson = fnArgs
            };
        });

    private const int MaxErrorDetailLength = 300;

    /// <summary>
    /// Extracts the human-readable error message from an OpenAI-style error
    /// response. Non-JSON bodies (HTML error pages, proxies) are replaced with
    /// a generic message instead of dumping the raw body into the chat.
    /// </summary>
    private static string ExtractErrorDetail(string responseString)
    {
        if (string.IsNullOrWhiteSpace(responseString))
            return "empty response";

        try
        {
            using var doc = JsonDocument.Parse(responseString);
            if (doc.RootElement.TryGetProperty("error", out JsonElement err) && err.TryGetProperty("message", out JsonElement msg))
            {
                return TruncateError(msg.GetString() ?? "unknown error");
            }
        }
        catch (JsonException)
        {
            // Non-JSON error response fallback
        }

        return "response was not valid JSON (check the API base URL and key)";
    }

    private static string TruncateError(string value)
    {
        if (value.Length <= MaxErrorDetailLength)
            return value;

        var cut = value.AsSpan(0, MaxErrorDetailLength);
        var lastSpace = cut.LastIndexOf(' ');
        var trimmed = lastSpace > 0 ? cut[..lastSpace].ToString() : cut.ToString();
        return trimmed + "…";
    }

    private static string GetChatCompletionsUrl(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/chat/completions";
    }

    private static string GetModelsUrl(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^"/chat/completions".Length].TrimEnd('/');
        }
        return $"{trimmed}/models";
    }
}
