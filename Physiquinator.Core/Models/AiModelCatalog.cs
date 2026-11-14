namespace Physiquinator.Core.Models;

public sealed record AiModelOption(string Id, string DisplayName, string Description, string Badge = "");

public static class AiModelCatalog
{
    private static readonly Dictionary<AiProviderType, List<AiModelOption>> ProviderModels = new()
    {
        [AiProviderType.OpenCodeGo] =
        [
            new("muse-spark-1.3-contributor", "Muse Spark 1.3 Contributor", "Fast multimodal and reasoning agent", "Recommended"),
            new("mimo-v2.5", "mimo-v2.5", "Flagship reasoning, coding, and workout tools", "Flagship"),
            new("qwen3.8-flash", "Qwen 3.8 Flash", "Fast response and tool execution", "Fast"),
            new("deepseek-v4.1-flash", "DeepSeek V4.1 Flash", "High intelligence and fast execution"),
            new("minimax-m3.1-flash-free", "MiniMax M3.1 Flash", "Efficient free-tier model", "Free"),
            new("glm-5.3", "GLM 5.3", "General knowledge and assistance"),
            new("gpt-4o", "GPT-4o", "Omni multimodal model"),
        ],
        [AiProviderType.OpenCode] =
        [
            new("muse-spark-1.3-contributor", "Muse Spark 1.3 Contributor", "Fast multimodal and reasoning agent", "Recommended"),
            new("mimo-v2.5", "mimo-v2.5", "Flagship reasoning, coding, and workout tools", "Flagship"),
            new("qwen3.8-flash", "Qwen 3.8 Flash", "Fast response and tool execution", "Fast"),
            new("deepseek-v4.1-flash", "DeepSeek V4.1 Flash", "High intelligence and fast execution"),
            new("minimax-m3.1-flash-free", "MiniMax M3.1 Flash", "Efficient free-tier model", "Free"),
            new("glm-5.3", "GLM 5.3", "General knowledge and assistance"),
        ],
        [AiProviderType.OpenRouter] =
        [
            new("anthropic/claude-3.7-sonnet", "Claude 3.7 Sonnet", "Advanced reasoning", "Recommended"),
            new("anthropic/claude-3.5-haiku", "Claude 3.5 Haiku", "Fast and lightweight"),
            new("google/gemini-2.0-flash-001", "Gemini 2.0 Flash", "Ultra-fast response"),
            new("deepseek/deepseek-chat", "DeepSeek V3", "Powerful general assistant"),
            new("meta-llama/llama-3.3-70b-instruct", "Llama 3.3 70B", "Open-weights intelligence"),
        ],
        [AiProviderType.OpenAI] =
        [
            new("gpt-4o", "GPT-4o", "Flagship multimodal intelligence", "Recommended"),
            new("gpt-4o-mini", "GPT-4o Mini", "Fast and cost-effective"),
            new("o3-mini", "o3-mini", "Advanced reasoning"),
        ],
        [AiProviderType.Groq] =
        [
            new("llama-3.3-70b-versatile", "Llama 3.3 70B Versatile", "Instant responses", "Fastest"),
            new("mixtral-8x7b-32768", "Mixtral 8x7B", "Long context window (32k)"),
        ],
        [AiProviderType.OllamaLocal] =
        [
            new("llama3.2", "Llama 3.2", "Standard local lightweight model"),
            new("deepseek-r1", "DeepSeek R1", "Local reasoning model"),
            new("qwen2.5-coder", "Qwen 2.5 Coder", "Local task assistant"),
            new("mistral", "Mistral 7B", "Balanced local model"),
        ],
    };

    public static IReadOnlyList<AiModelOption> GetModelsForProvider(AiProviderType provider)
    {
        if (ProviderModels.TryGetValue(provider, out var models))
            return models;
        return ProviderModels[AiProviderType.OpenCodeGo];
    }

    public static string GetModelDisplayName(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return modelId ?? string.Empty;
        foreach (var list in ProviderModels.Values)
        {
            var found = list.FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
            if (found is not null)
                return found.DisplayName;
        }
        var parts = modelId.Split('/');
        return parts.Length > 1 ? parts[^1] : modelId;
    }

    public static string GetModelTooltip(string displayName, string? description) =>
        string.IsNullOrWhiteSpace(description) ? displayName : $"{displayName}: {description}";
}
