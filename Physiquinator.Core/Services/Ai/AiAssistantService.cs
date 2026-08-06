using Physiquinator.Core.Models;
using System.Globalization;
using System.Text;

namespace Physiquinator.Core.Services.Ai;

public sealed class AiAssistantService(
    IAppPreferences preferences,
    UserProfileService userProfileService,
    OpenAiCompatibleClient client,
    AiToolRegistry toolRegistry)
{
    private readonly List<AiChatMessage> _messages = [];

    // Persistent API history maintains the full message exchange (including tool call/response pairs)
    // across user turns so the provider never sees an assistant tool_calls message without matching tool responses.
    private List<AiChatMessage> _apiHistory = [];

    public event Action? OnMessagesChanged;

    public IReadOnlyList<AiChatMessage> Messages => _messages.AsReadOnly();

    public AiProviderSettings GetSettings()
    {
        var providerStr = preferences.Get(PreferenceKeys.AiProvider, AiProviderType.OpenAI.ToString());
        Enum.TryParse<AiProviderType>(providerStr, true, out AiProviderType provider);

        return new AiProviderSettings
        {
            Enabled = preferences.Get(PreferenceKeys.AiEnabled, true),
            Provider = provider,

            BaseUrl = preferences.Get(PreferenceKeys.AiBaseUrl, "https://api.openai.com/v1"),
            ApiKey = preferences.Get(PreferenceKeys.AiApiKey, string.Empty),
            ModelName = preferences.Get(PreferenceKeys.AiModelName, "gpt-4o-mini"),
            CustomSystemPrompt = preferences.Get(PreferenceKeys.AiSystemPrompt, string.Empty)
        };
    }

    public void SaveSettings(AiProviderSettings settings)
    {
        preferences.Set(PreferenceKeys.AiEnabled, settings.Enabled);
        preferences.Set(PreferenceKeys.AiProvider, settings.Provider.ToString());
        preferences.Set(PreferenceKeys.AiBaseUrl, settings.BaseUrl);
        preferences.Set(PreferenceKeys.AiApiKey, settings.ApiKey);
        preferences.Set(PreferenceKeys.AiModelName, settings.ModelName);
        preferences.Set(PreferenceKeys.AiSystemPrompt, settings.CustomSystemPrompt);
    }

    public Task<List<string>> FetchAvailableModelsAsync()
    {
        AiProviderSettings settings = GetSettings();
        return client.GetAvailableModelsAsync(settings);
    }

    public void ClearHistory()
    {
        _messages.Clear();
        _apiHistory = [];
        OnMessagesChanged?.Invoke();
    }

    public async Task SendUserMessageAsync(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt)) return;

        AddUserMessage(userPrompt);
        AiChatMessage assistantMessage = AddInitialAssistantMessage();

        AiProviderSettings settings = GetSettings();
        if (!IsConfigured(settings))
        {
            SetNotConfiguredError(assistantMessage);
            return;
        }

        // Re-initialise the persistent API history with the fresh system prompt + carry-over history
        _apiHistory = BuildApiMessageHistory(settings);
        var toolsSchema = toolRegistry.GetOpenAiToolsSchema();

        await ExecuteConversationLoopAsync(settings, toolsSchema, assistantMessage);
    }


    private static bool IsConfigured(AiProviderSettings settings) =>
        settings.Enabled && (!string.IsNullOrWhiteSpace(settings.ApiKey) || settings.Provider == AiProviderType.OllamaLocal);

    private void AddUserMessage(string userPrompt)
    {
        _messages.Add(new AiChatMessage
        {
            Role = AiMessageRole.User,
            Content = userPrompt.Trim(),
            Timestamp = DateTime.Now
        });
    }

    private AiChatMessage AddInitialAssistantMessage()
    {
        var msg = new AiChatMessage
        {
            Role = AiMessageRole.Assistant,
            IsThinking = true,
            Content = string.Empty,
            Timestamp = DateTime.Now
        };
        _messages.Add(msg);
        OnMessagesChanged?.Invoke();
        return msg;
    }

    private void SetNotConfiguredError(AiChatMessage assistantMessage)
    {
        assistantMessage.IsThinking = false;
        assistantMessage.IsError = true;
        assistantMessage.Content = "AI Assistant is not configured. Please enable AI Assistant and provide an API Key in Settings.";
        OnMessagesChanged?.Invoke();
    }

    private async Task ExecuteConversationLoopAsync(
        AiProviderSettings settings,
        object? toolsSchema,
        AiChatMessage currentAssistantMessage)
    {
        const int maxToolLoops = 5;
        AiChatMessage assistantMsg = currentAssistantMessage;

        for (var loop = 0; loop < maxToolLoops; loop++)
        {
            (List<AiToolCallInfo>? executedToolCalls, var errorMessage) = await ConsumeResponseStreamAsync(settings, toolsSchema, assistantMsg);

            if (!string.IsNullOrEmpty(errorMessage))
            {
                SetStreamError(assistantMsg, errorMessage);
                return;
            }

            assistantMsg.IsThinking = false;
            OnMessagesChanged?.Invoke();

            if (executedToolCalls.Count == 0)
            {
                // No tool calls — this is the final assistant response. Persist it to _apiHistory
                // so the next user turn has full context without re-referencing _messages.
                _apiHistory.Add(new AiChatMessage
                {
                    Role = AiMessageRole.Assistant,
                    Content = assistantMsg.Content,
                    ReasoningContent = assistantMsg.ReasoningContent,
                    Timestamp = assistantMsg.Timestamp
                });
                return;
            }

            assistantMsg.ToolCalls = executedToolCalls;
            OnMessagesChanged?.Invoke();

            await ProcessToolExecutionsAsync(assistantMsg.Content, assistantMsg.ReasoningContent, executedToolCalls);

            assistantMsg = AddInitialAssistantMessage();
        }

        SetMaxLoopsReached(assistantMsg);
    }


    private async Task<(List<AiToolCallInfo> ToolCalls, string? ErrorMessage)> ConsumeResponseStreamAsync(
        AiProviderSettings settings,
        object? toolsSchema,
        AiChatMessage assistantMessage)
    {
        var toolCallAccumulator = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        var contentBuilder = new StringBuilder();
        var reasoningBuilder = new StringBuilder();
        string? errorMessage = null;

        await foreach (StreamingChatChunk chunk in client.StreamChatCompletionAsync(settings, _apiHistory, toolsSchema))
        {

            if (chunk.IsError)
            {
                errorMessage = chunk.ErrorMessage;
                break;
            }

            ProcessChunkDelta(chunk, assistantMessage, contentBuilder, reasoningBuilder);
            AccumulateToolCalls(chunk, toolCallAccumulator);
        }

        var executedToolCalls = toolCallAccumulator.Values.Select(v => new AiToolCallInfo
        {
            Id = string.IsNullOrEmpty(v.Id) ? Guid.NewGuid().ToString("N") : v.Id,
            Name = v.Name,
            ArgumentsJson = v.Args.ToString()
        }).ToList();

        return (executedToolCalls, errorMessage);
    }

    private void ProcessChunkDelta(
        StreamingChatChunk chunk,
        AiChatMessage assistantMessage,
        StringBuilder contentBuilder,
        StringBuilder reasoningBuilder)
    {
        var updated = false;
        if (!string.IsNullOrEmpty(chunk.ReasoningContent))
        {
            reasoningBuilder.Append(chunk.ReasoningContent);
            assistantMessage.ReasoningContent = reasoningBuilder.ToString();
            updated = true;
        }

        if (!string.IsNullOrEmpty(chunk.DeltaContent))
        {
            if (assistantMessage.IsThinking)
            {
                assistantMessage.IsThinking = false;
            }
            contentBuilder.Append(chunk.DeltaContent);
            assistantMessage.Content = contentBuilder.ToString();
            updated = true;
        }

        if (updated)
        {
            OnMessagesChanged?.Invoke();
        }
    }

    private static void AccumulateToolCalls(
        StreamingChatChunk chunk,
        Dictionary<int, (string Id, string Name, StringBuilder Args)> accumulator)
    {
        if (chunk.ToolCalls.Count == 0) return;

        for (var i = 0; i < chunk.ToolCalls.Count; i++)
        {
            AiToolCallInfo tc = chunk.ToolCalls[i];
            if (!accumulator.TryGetValue(i, out (string Id, string Name, StringBuilder Args) acc))
            {
                acc = (tc.Id, tc.Name, new StringBuilder());
            }

            var newId = !string.IsNullOrEmpty(tc.Id) ? tc.Id : acc.Id;
            var newName = !string.IsNullOrEmpty(tc.Name) ? tc.Name : acc.Name;
            if (!string.IsNullOrEmpty(tc.ArgumentsJson))
            {
                acc.Args.Append(tc.ArgumentsJson);
            }

            accumulator[i] = (newId, newName, acc.Args);
        }
    }

    private void SetStreamError(AiChatMessage assistantMessage, string errorMessage)
    {
        assistantMessage.IsThinking = false;
        assistantMessage.IsError = true;
        assistantMessage.Content = $"Error: {errorMessage}";
        OnMessagesChanged?.Invoke();
    }

    private void SetMaxLoopsReached(AiChatMessage assistantMessage)
    {
        assistantMessage.IsThinking = false;
        assistantMessage.Content = "Reached maximum tool execution steps.";
        OnMessagesChanged?.Invoke();
    }

    private async Task ProcessToolExecutionsAsync(
        string assistantContent,
        string reasoningContent,
        List<AiToolCallInfo> toolCalls)
    {
        _apiHistory.Add(new AiChatMessage
        {
            Role = AiMessageRole.Assistant,
            Content = assistantContent,
            ReasoningContent = reasoningContent,
            ToolCalls = toolCalls,
            Timestamp = DateTime.Now
        });

        foreach (AiToolCallInfo toolCall in toolCalls)
        {
            var toolResultJson = await toolRegistry.ExecuteToolAsync(toolCall.Name, toolCall.ArgumentsJson);
            _apiHistory.Add(new AiChatMessage
            {
                Role = AiMessageRole.Tool,
                ToolCallId = toolCall.Id,
                ToolName = toolCall.Name,
                Content = toolResultJson,
                Timestamp = DateTime.Now
            });
        }
    }



    private List<AiChatMessage> BuildApiMessageHistory(AiProviderSettings settings)
    {
        UserProfile activeProfile = userProfileService.GetActiveProfile();
        var activeBw = activeProfile.BodyweightKg?.ToString("F1", CultureInfo.InvariantCulture) ?? "not logged";

        var systemPrompt = $"""
            You are Physiquinator AI, an intelligent fitness, workout, and bodyweight assistant inside the Physiquinator workout tracking application.
            Current Active User Profile: {activeProfile.Name}
            Current Active Bodyweight: {activeBw} kg
            Current System Time: {DateTime.Now:F}

            Capabilities:
            - Analyze, modify, or create workout plans.
            - Record bodyweight logs.
            - Tweak app settings and theme.
            - Answer questions regarding personal records and history.

            Guidelines:
            - Be concise, clear, encouraging, and informative.
            - Format structured workout tables and data using GitHub Markdown syntax.
            """;

        if (!string.IsNullOrWhiteSpace(settings.CustomSystemPrompt))
        {
            systemPrompt += $"\n\nUser Custom Instructions:\n{settings.CustomSystemPrompt.Trim()}";
        }

        var systemMessage = new AiChatMessage
        {
            Role = AiMessageRole.System,
            Content = systemPrompt
        };

        // If we already have a persistent API history, update the system message and append the latest user message.
        if (_apiHistory.Count > 0)
        {
            // Replace existing system message with fresh one (updated time, bodyweight, etc.)
            var fresh = new List<AiChatMessage> { systemMessage };
            fresh.AddRange(_apiHistory.Where(m => m.Role != AiMessageRole.System));

            // Append the latest user message that was just added to _messages
            AiChatMessage? latestUser = _messages.LastOrDefault(m => m.Role == AiMessageRole.User);
            if (latestUser != null)
            {
                fresh.Add(new AiChatMessage
                {
                    Role = AiMessageRole.User,
                    Content = latestUser.Content,
                    Timestamp = latestUser.Timestamp
                });
            }

            return fresh;
        }

        // First conversation turn: build from visible messages, excluding in-progress/error messages
        var apiMessages = new List<AiChatMessage> { systemMessage };
        apiMessages.AddRange(_messages.Where(m => !m.IsThinking && !m.IsError));
        return apiMessages;
    }
}

