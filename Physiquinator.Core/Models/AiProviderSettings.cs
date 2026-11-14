namespace Physiquinator.Core.Models;

public enum AiProviderType
{
    OpenAI,
    OpenRouter,
    OpenCode,
    OpenCodeGo,
    Groq,
    OllamaLocal,
    Custom
}

public class AiProviderSettings
{
    public const string DefaultOpenAiEndpoint = "https://api.openai.com/v1";
    public const string DefaultOpenRouterEndpoint = "https://openrouter.ai/api/v1";
    public const string DefaultOpenCodeEndpoint = "https://opencode.ai/inference/openai/v1";
    public const string DefaultOpenCodeGoEndpoint = "https://opencode.ai/inference/go/openai/v1";
    public const string DefaultGroqEndpoint = "https://api.groq.com/openai/v1";
    public const string DefaultOllamaEndpoint = "http://localhost:11434/v1";

    public bool Enabled { get; set; } = true;

    public AiProviderType Provider { get; set; } = AiProviderType.OpenCodeGo;
    public string BaseUrl { get; set; } = DefaultOpenCodeGoEndpoint;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = "muse-spark-1.3-contributor";
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
            AiProviderType.OpenAI => DefaultOpenAiEndpoint,
            AiProviderType.OpenRouter => DefaultOpenRouterEndpoint,
            AiProviderType.OpenCode => DefaultOpenCodeEndpoint,
            AiProviderType.OpenCodeGo => DefaultOpenCodeGoEndpoint,
            AiProviderType.Groq => DefaultGroqEndpoint,
            AiProviderType.OllamaLocal => DefaultOllamaEndpoint,
            _ => DefaultOpenAiEndpoint
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
            AiProviderType.OpenRouter => "anthropic/claude-3.7-sonnet",
            AiProviderType.OpenCode => "muse-spark-1.3-contributor",
            AiProviderType.OpenCodeGo => "muse-spark-1.3-contributor",
            AiProviderType.Groq => "llama-3.3-70b-versatile",
            AiProviderType.OllamaLocal => "llama3.2",
            _ => "gpt-4o-mini"
        };
    }

    public AiProviderSettings Normalize()
    {
        BaseUrl = string.IsNullOrWhiteSpace(BaseUrl) ? GetDefaultBaseUrl(Provider) : BaseUrl.Trim().TrimEnd('/');
        ModelName = string.IsNullOrWhiteSpace(ModelName) ? GetDefaultModel(Provider) : ModelName.Trim();
        ApiKey = ApiKey?.Trim() ?? string.Empty;
        if (CustomSystemPrompt is not null && CustomSystemPrompt.Length > 2000)
        {
            CustomSystemPrompt = CustomSystemPrompt[..2000];
        }
        return this;
    }
}
