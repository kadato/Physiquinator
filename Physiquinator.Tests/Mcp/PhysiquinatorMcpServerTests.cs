using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Physiquinator.Tests.TestDoubles;
using Physiquinator.Web.Mcp;
using System.IO.Pipelines;
using System.Text.Json;
using Xunit;

namespace Physiquinator.Tests.Mcp;

public class PhysiquinatorMcpServerTests
{
    [Fact]
    public async Task ListTools_ExposesAllRegistryToolsWithSchemasAndAnnotations()
    {
        await using McpFixture fixture = await McpFixture.CreateAsync();

        IList<McpClientTool> tools = await fixture.Client.ListToolsAsync();

        Assert.Contains(tools, t => t.Name == "get_workout_plans");
        Assert.Contains(tools, t => t.Name == "create_workout_plan");
        Assert.Contains(tools, t => t.Name == "delete_workout_plan");
        Assert.Contains(tools, t => t.Name == "log_bodyweight_entry");
        Assert.Contains(tools, t => t.Name == "generate_deload_plan");

        McpClientTool listTool = Assert.Single(tools, t => t.Name == "get_workout_plans");
        Assert.False(string.IsNullOrEmpty(listTool.Description));
        Assert.Equal("object", listTool.JsonSchema.GetProperty("type").GetString());
        Assert.True(listTool.ProtocolTool.Annotations?.ReadOnlyHint);

        McpClientTool deleteTool = Assert.Single(tools, t => t.Name == "delete_workout_plan");
        Assert.True(deleteTool.ProtocolTool.Annotations?.DestructiveHint);
    }

    [Fact]
    public async Task GetWorkoutPlans_ReturnsSeededPlanWithStructuredContent()
    {
        await using McpFixture fixture = await McpFixture.CreateAsync();
        var plan = new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = "Push Hypertrophy",
            Exercises =
            [
                new ExercisePlan { Id = Guid.NewGuid(), Name = "Bench Press", SetCount = 4, DefaultReps = 8, DefaultWeightKg = 80.0 }
            ]
        };
        await fixture.PlanService.SavePlanAsync(plan);

        McpClientTool tool = Assert.Single(await fixture.Client.ListToolsAsync(), t => t.Name == "get_workout_plans");
        CallToolResult result = await tool.CallAsync();

        Assert.False(result.IsError.GetValueOrDefault());
        var text = Assert.Single(result.Content.OfType<TextContentBlock>()).Text;
        Assert.Contains("Push Hypertrophy", text);
        Assert.NotNull(result.StructuredContent);
    }

    [Fact]
    public async Task CreateWorkoutPlan_PersistsPlanThroughRegistry()
    {
        await using McpFixture fixture = await McpFixture.CreateAsync();

        McpClientTool tool = Assert.Single(await fixture.Client.ListToolsAsync(), t => t.Name == "create_workout_plan");
        CallToolResult result = await tool.CallAsync(new Dictionary<string, object?>
        {
            ["name"] = "Leg Day",
            ["exercises"] = new[]
            {
                new { name = "Squat", targetSets = 5, targetReps = 5 }
            }
        });

        Assert.False(result.IsError.GetValueOrDefault());
        List<WorkoutPlan> plans = await fixture.PlanService.GetAllPlansAsync();
        Assert.Single(plans);
        Assert.Equal("Leg Day", plans[0].Name);
    }

    [Fact]
    public async Task DeleteWorkoutPlan_RunsAfterUserConfirmation()
    {
        await using McpFixture fixture = await McpFixture.CreateAsync(confirm: true);
        var plan = new WorkoutPlan { Id = Guid.NewGuid(), Name = "To Delete" };
        await fixture.PlanService.SavePlanAsync(plan);

        McpClientTool tool = Assert.Single(await fixture.Client.ListToolsAsync(), t => t.Name == "delete_workout_plan");
        CallToolResult result = await tool.CallAsync(new Dictionary<string, object?> { ["planId"] = plan.Id.ToString() });

        Assert.False(result.IsError.GetValueOrDefault());
        Assert.Null(await fixture.PlanService.GetPlanAsync(plan.Id));
    }

    [Fact]
    public async Task DeleteWorkoutPlan_IsSkippedWhenUserDeclinesConfirmation()
    {
        await using McpFixture fixture = await McpFixture.CreateAsync(confirm: false);
        var plan = new WorkoutPlan { Id = Guid.NewGuid(), Name = "Keep Me" };
        await fixture.PlanService.SavePlanAsync(plan);

        McpClientTool tool = Assert.Single(await fixture.Client.ListToolsAsync(), t => t.Name == "delete_workout_plan");
        CallToolResult result = await tool.CallAsync(new Dictionary<string, object?> { ["planId"] = plan.Id.ToString() });

        Assert.False(result.IsError.GetValueOrDefault());
        Assert.Contains("did not confirm", Assert.Single(result.Content.OfType<TextContentBlock>()).Text);
        Assert.NotNull(await fixture.PlanService.GetPlanAsync(plan.Id));
    }

    [Fact]
    public async Task UnknownTool_ReturnsErrorResult()
    {
        await using McpFixture fixture = await McpFixture.CreateAsync();

        CallToolResult result = await fixture.Client.CallToolAsync("does_not_exist", null);

        Assert.True(result.IsError.GetValueOrDefault());
        Assert.Contains("does_not_exist", Assert.Single(result.Content.OfType<TextContentBlock>()).Text);
    }

    private sealed class McpFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly Task _serverTask;
        private readonly McpServer _server;

        public McpClient Client { get; }

        public WorkoutPlanService PlanService => _provider.GetRequiredService<WorkoutPlanService>();

        private McpFixture(
            ServiceProvider provider,
            McpServer server,
            Task serverTask,
            McpClient client)
        {
            _provider = provider;
            _server = server;
            _serverTask = serverTask;
            Client = client;
        }

        public static async Task<McpFixture> CreateAsync(bool? confirm = null)
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"physiquinator_mcp_test_{Guid.NewGuid():N}.db");
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IJSRuntime>(new NoopJSRuntime());
            services.AddPhysiquinatorServices(new InMemoryPreferences(), new TempDbPathProvider(dbPath));
            ServiceProvider provider = services.BuildServiceProvider();
            var options = new McpServerOptions
            {
                ServerInstructions = "Test server.",
                Capabilities = new ServerCapabilities { Tools = new ToolsCapability() },
                Handlers = new McpServerHandlers
                {
                    ListToolsHandler = PhysiquinatorMcpTools.ListToolsAsync,
                    CallToolHandler = PhysiquinatorMcpTools.CallToolAsync
                },
                Filters = new McpServerFilters
                {
                    Request = new McpRequestFilters
                    {
                        CallToolFilters = [PhysiquinatorMcpTools.CallToolTelemetryFilter]
                    }
                }
            };

            var clientToServer = new Pipe();
            var serverToClient = new Pipe();

            var server = McpServer.Create(
                new StreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream()),
                options,
                null,
                provider);
            Task serverTask = server.RunAsync();

            var clientOptions = new McpClientOptions();
            if (confirm is not null)
            {
                clientOptions.Capabilities = new ClientCapabilities { Elicitation = new ElicitationCapability() };
                clientOptions.Handlers = new McpClientHandlers
                {
                    ElicitationHandler = (request, ct) => new ValueTask<ElicitResult>(new ElicitResult
                    {
                        Action = confirm.Value ? "accept" : "reject",
                        Content = new Dictionary<string, JsonElement>
                        {
                            ["confirm"] = JsonSerializer.SerializeToElement(confirm.Value)
                        }
                    })
                };
            }

            McpClient client = await McpClient.CreateAsync(
                new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
                clientOptions);

            return new McpFixture(provider, server, serverTask, client);
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _server.DisposeAsync();
            await _serverTask;
            await _provider.DisposeAsync();
        }
    }
}
