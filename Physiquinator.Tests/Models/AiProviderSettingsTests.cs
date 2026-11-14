using Physiquinator.Core.Models;
using Xunit;

namespace Physiquinator.Tests.Models;

public class AiProviderSettingsTests
{
    [Theory]
    [InlineData(AiProviderType.OpenAI)]
    [InlineData(AiProviderType.OpenRouter)]
    [InlineData(AiProviderType.OpenCode)]
    [InlineData(AiProviderType.OpenCodeGo)]
    [InlineData(AiProviderType.Groq)]
    [InlineData(AiProviderType.OllamaLocal)]
    [InlineData(AiProviderType.Custom)]
    public void Every_provider_has_a_default_base_url_and_model(AiProviderType provider)
    {
        Assert.False(string.IsNullOrWhiteSpace(AiProviderSettings.GetDefaultBaseUrl(provider)));
        Assert.False(string.IsNullOrWhiteSpace(AiProviderSettings.GetDefaultModel(provider)));
    }

    [Fact]
    public void OpenCode_defaults_point_at_a_real_catalog_entry()
    {
        // Base URL stays a root URL. The client appends /chat/completions and /models.
        Assert.Equal("https://opencode.ai/inference/go/openai/v1", AiProviderSettings.GetDefaultBaseUrl(AiProviderType.OpenCodeGo));
        Assert.Equal("https://opencode.ai/inference/openai/v1", AiProviderSettings.GetDefaultBaseUrl(AiProviderType.OpenCode));
        Assert.Equal("muse-spark-1.3-contributor", AiProviderSettings.GetDefaultModel(AiProviderType.OpenCodeGo));
        Assert.Equal("muse-spark-1.3-contributor", AiProviderSettings.GetDefaultModel(AiProviderType.OpenCode));
    }

    [Fact]
    public void Base_url_never_includes_chat_completions_suffix()
    {
        foreach (AiProviderType provider in Enum.GetValues<AiProviderType>())
            Assert.DoesNotContain("/chat/completions", AiProviderSettings.GetDefaultBaseUrl(provider), StringComparison.OrdinalIgnoreCase);
    }
}
