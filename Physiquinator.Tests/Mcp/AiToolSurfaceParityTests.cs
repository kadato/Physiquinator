using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using ModelContextProtocol.Protocol;
using Physiquinator.Core.Services;
using Physiquinator.Core.Services.Ai;
using Physiquinator.Tests.TestDoubles;
using Physiquinator.Web.Mcp;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Physiquinator.Tests.Mcp;

/// <summary>
/// Locks the in-app OpenAI-style chat tool surface and the MCP tool surface together:
/// both are served from the same <see cref="AiToolRegistry"/>, so names, descriptions,
/// and parameter schemas must never drift.
/// </summary>
public class AiToolSurfaceParityTests
{
    [Fact]
    public void McpSurface_MatchesInAppChatSurface()
    {
        AiToolRegistry registry = BuildRegistry();

        var openAiTools = JsonSerializer.SerializeToElement(registry.GetOpenAiToolsSchema())
            .EnumerateArray()
            .ToDictionary(
                t => t.GetProperty("function").GetProperty("name").GetString()!,
                t => t.GetProperty("function"),
                StringComparer.Ordinal);

        var mcpTools = registry.GetAllTools().Select(PhysiquinatorMcpTools.CreateTool).ToList();

        Assert.Equal(openAiTools.Count, mcpTools.Count);

        foreach (Tool? mcpTool in mcpTools)
        {
            Assert.True(openAiTools.TryGetValue(mcpTool.Name, out JsonElement openAiFunction),
                $"Chat has no tool named '{mcpTool.Name}'.");
            Assert.Equal(openAiFunction.GetProperty("description").GetString(), mcpTool.Description);
            Assert.True(JsonNode.DeepEquals(
                JsonNode.Parse(openAiFunction.GetProperty("parameters").GetRawText()),
                JsonNode.Parse(mcpTool.InputSchema.GetRawText())),
                $"Schema drift for tool '{mcpTool.Name}'.");
        }
    }

    private static AiToolRegistry BuildRegistry()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiquinator_parity_{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime>(new NoopJSRuntime());
        services.AddPhysiquinatorServices(new InMemoryPreferences(), new TempDbPathProvider(dbPath));
        return services.BuildServiceProvider().GetRequiredService<AiToolRegistry>();
    }
}
