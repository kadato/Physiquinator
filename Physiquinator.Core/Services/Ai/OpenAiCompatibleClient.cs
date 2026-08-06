using Physiquinator.Core.Models;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Physiquinator.Core.Services.Ai;

public sealed class OpenAiCompatibleResponse
{
    public string AssistantContent { get; set; } = string.Empty;
    public string ReasoningContent { get; set; } = string.Empty;
    public List<AiToolCallInfo> ToolCalls { get; set; } = [];
    public bool IsError { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed class StreamingChatChunk
{
    public string DeltaContent { get; set; } = string.Empty;
    public string ReasoningContent { get; set; } = string.Empty;
    public List<AiToolCallInfo> ToolCalls { get; set; } = [];
    public bool IsError { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}


public sealed class OpenAiCompatibleClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<List<string>> GetAvailableModelsAsync(AiProviderSettings settings)
    {
        try
        {
            var modelsUrl = GetModelsUrl(settings.GetEffectiveBaseUrl());
            using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);

            AddAuthenticationHeader(request, settings);
            AddProviderHeaders(request, settings);

            var httpResponse = await httpClient.SendAsync(request);
            if (!httpResponse.IsSuccessStatusCode) return [];

            var json = await httpResponse.Content.ReadAsStringAsync();
            return ParseModelsJson(json);
        }
        catch
        {
            return [];
        }
    }

    private static List<string> ParseModelsJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var dataElem) || dataElem.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var modelList = new List<string>();
        foreach (var item in dataElem.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var idP) && idP.ValueKind == JsonValueKind.String && idP.GetString() is { Length: > 0 } idStr)
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
        using var request = BuildHttpRequest(endpointUrl, settings, messageHistory, toolsSchema, stream: true);

        HttpResponseMessage? response = null;
        string? connectionError = null;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex)
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

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            await foreach (var chunk in ReadStreamChunksAsync(reader, cancellationToken))
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

            var chunk = TryParseStreamingChunk(dataJson);
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
        object? toolsSchema = null)
    {
        var response = new OpenAiCompatibleResponse();

        try
        {
            var endpointUrl = GetChatCompletionsUrl(settings.GetEffectiveBaseUrl());
            using var request = BuildHttpRequest(endpointUrl, settings, messageHistory, toolsSchema, stream: false);

            var httpResponse = await httpClient.SendAsync(request);
            var responseString = await httpResponse.Content.ReadAsStringAsync();

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
            request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/tothKarolyDavid/Physiquinator");
            request.Headers.TryAddWithoutValidation("X-Title", "Physiquinator Fitness App");
        }
    }

    private static StreamingChatChunk? ParseStreamingChunk(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var choice = choices[0];
        if (!choice.TryGetProperty("delta", out var delta))
        {
            return null;
        }

        var chunk = new StreamingChatChunk();
        if (delta.TryGetProperty("content", out var contentElem) && contentElem.ValueKind == JsonValueKind.String)
        {
            chunk.DeltaContent = contentElem.GetString() ?? string.Empty;
        }

        if (delta.TryGetProperty("reasoning_content", out var rcElem) && rcElem.ValueKind == JsonValueKind.String)
        {
            chunk.ReasoningContent = rcElem.GetString() ?? string.Empty;
        }
        else if (delta.TryGetProperty("thinking", out var thElem) && thElem.ValueKind == JsonValueKind.String)
        {
            chunk.ReasoningContent = thElem.GetString() ?? string.Empty;
        }

        ParseStreamingToolCalls(delta, chunk.ToolCalls);
        return chunk;
    }

    private static void ParseStreamingToolCalls(JsonElement delta, List<AiToolCallInfo> toolCalls)
    {
        if (!delta.TryGetProperty("tool_calls", out var toolCallsElem) || toolCallsElem.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var tc in toolCallsElem.EnumerateArray())
        {
            var tcId = tc.TryGetProperty("id", out var idp) ? idp.GetString() ?? string.Empty : string.Empty;
            var fnName = string.Empty;
            var fnArgs = string.Empty;

            if (tc.TryGetProperty("function", out var fn))
            {
                if (fn.TryGetProperty("name", out var np)) fnName = np.GetString() ?? string.Empty;
                if (fn.TryGetProperty("arguments", out var ap)) fnArgs = ap.GetString() ?? string.Empty;
            }

            toolCalls.Add(new AiToolCallInfo { Id = tcId, Name = fnName, ArgumentsJson = fnArgs });
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
                assistantDict["reasoning_content"] = m.ReasoningContent;
            }
            else if (m.ToolCalls != null && m.ToolCalls.Count > 0)
            {
                // OpenCode / DeepSeek-R1 reasoning mode requires reasoning_content on assistant messages in multi-turn tool loops
                assistantDict["reasoning_content"] = "";
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
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            response.IsError = true;
            response.ErrorMessage = "Received empty choice response from AI provider.";
            return;
        }

        var messageElem = choices[0].GetProperty("message");
        if (messageElem.TryGetProperty("content", out var contentElem) && contentElem.ValueKind == JsonValueKind.String)
        {
            response.AssistantContent = contentElem.GetString() ?? string.Empty;
        }

        if (messageElem.TryGetProperty("reasoning_content", out var rcElem) && rcElem.ValueKind == JsonValueKind.String)
        {
            response.ReasoningContent = rcElem.GetString() ?? string.Empty;
        }
        else if (messageElem.TryGetProperty("thinking", out var thElem) && thElem.ValueKind == JsonValueKind.String)
        {
            response.ReasoningContent = thElem.GetString() ?? string.Empty;
        }

        ParseJsonResponseToolCalls(messageElem, response.ToolCalls);
    }


    private static void ParseJsonResponseToolCalls(JsonElement messageElem, List<AiToolCallInfo> toolCalls)
    {
        if (!messageElem.TryGetProperty("tool_calls", out var toolCallsElem) || toolCallsElem.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var tc in toolCallsElem.EnumerateArray())
        {
            var tcId = tc.TryGetProperty("id", out var idp) ? idp.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
            var fn = tc.GetProperty("function");
            var fnName = fn.GetProperty("name").GetString() ?? string.Empty;
            var fnArgs = fn.GetProperty("arguments").GetString() ?? "{}";

            toolCalls.Add(new AiToolCallInfo
            {
                Id = tcId,
                Name = fnName,
                ArgumentsJson = fnArgs
            });
        }
    }

    private static string ExtractErrorDetail(string responseString)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseString);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg))
            {
                return msg.GetString() ?? responseString;
            }
        }
        catch (JsonException)
        {
            // Non-JSON error response fallback
        }
        return responseString;
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
