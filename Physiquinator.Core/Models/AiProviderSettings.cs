namespace Physiquinator.Core.Models;

public enum AiProviderType
{
    OpenAI,
    OpenRouter,
    OpenCode,
    OllamaLocal,
    Custom
}

public class AiProviderSettings
{
    public bool Enabled { get; set; } = true;

    public AiProviderType Provider { get; set; } = AiProviderType.OpenAI;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = "gpt-4o-mini";
    public string CustomSystemPrompt { get; set; } = string.Empty;

    public string GetEffectiveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
            return BaseUrl.TrimEnd('/');

        return GetDefaultBaseUrl(Provider);
    }

    public string GetDefaultBaseUrl() => GetDefaultBaseUrl(Provider);

    public static string GetDefaultBaseUrl(AiProviderType provider)
    {
        return provider switch
        {
            AiProviderType.OpenAI => "https://api.openai.com/v1",
            AiProviderType.OpenRouter => "https://openrouter.ai/api/v1",
            AiProviderType.OpenCode => "https://opencode.ai/zen/go/v1/chat/completions",
            AiProviderType.OllamaLocal => "http://localhost:11434/v1",
            _ => "https://api.openai.com/v1"
        };
    }

    public string GetEffectiveModel()
    {
        if (!string.IsNullOrWhiteSpace(ModelName))
            return ModelName.Trim();

        return GetDefaultModel(Provider);
    }

    public string GetDefaultModel() => GetDefaultModel(Provider);


    public static string GetDefaultModel(AiProviderType provider)
    {
        return provider switch
        {
            AiProviderType.OpenAI => "gpt-4o-mini",
            AiProviderType.OpenRouter => "anthropic/claude-3.5-sonnet",
            AiProviderType.OpenCode => "deepseek-v4-flash",
            AiProviderType.OllamaLocal => "llama3.2",
            _ => "gpt-4o-mini"
        };
    }
}
