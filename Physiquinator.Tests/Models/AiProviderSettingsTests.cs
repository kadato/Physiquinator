using Physiquinator.Core.Models;
using Xunit;

namespace Physiquinator.Tests.Models;

public class AiProviderSettingsTests
{
    [Theory]
    [InlineData(AiProviderType.OpenAI)]
    [InlineData(AiProviderType.OpenRouter)]
    [InlineData(AiProviderType.OpenCode)]
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
        // The OpenCode preset must use the provider's own model naming
        // For example deepseek-v4-flash, not a made-up slug.
        Assert.Equal("https://opencode.ai/zen/go/v1/chat/completions", AiProviderSettings.GetDefaultBaseUrl(AiProviderType.OpenCode));
        Assert.Equal("deepseek-v4-flash", AiProviderSettings.GetDefaultModel(AiProviderType.OpenCode));
    }
}
