using Physiquinator.Core.Models;
using Physiquinator.Core.Services.Ai;
using System.Net;
using System.Text;
using Xunit;

namespace Physiquinator.Tests.Services.Ai;

public class OpenAiCompatibleClientTests
{
    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private static OpenAiCompatibleClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new HttpClient(new StubHttpMessageHandler(responder)));

    private static HttpResponseMessage SseResponse(string body) =>
        new(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(body))) };

    private static AiProviderSettings Settings() => new()
    {
        BaseUrl = "https://example.test/v1",
        ApiKey = "test-key",
        ModelName = "test-model"
    };

    [Fact]
    public async Task StreamChatCompletionAsync_AssemblesContentAcrossDeltas()
    {
        const string sse = """
            data: {"choices":[{"delta":{"role":"assistant","content":"Hello"}}]}

            data: {"choices":[{"delta":{"content":", world"}}]}

            data: {"choices":[{"delta":{"content":"!"}}]}

            data: [DONE]
            """;
        OpenAiCompatibleClient client = CreateClient(_ => SseResponse(sse));

        var chunks = new List<StreamingChatChunk>();
        await foreach (StreamingChatChunk chunk in client.StreamChatCompletionAsync(Settings(), []))
        {
            chunks.Add(chunk);
        }

        Assert.Equal("Hello, world!", string.Concat(chunks.Select(c => c.DeltaContent)));
        Assert.All(chunks, c => Assert.False(c.IsError));
    }

    [Fact]
    public async Task StreamChatCompletionAsync_ParsesToolCallDeltas()
    {
        const string sse = """
            data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_abc","function":{"name":"create_workout_plan","arguments":"{\"name\":\"Push Hypertrophy\",\"exercises\":[{\"name\":\"Bench Press\"}]}"}}]}}]}

            data: [DONE]
            """;
        OpenAiCompatibleClient client = CreateClient(_ => SseResponse(sse));

        var chunks = new List<StreamingChatChunk>();
        await foreach (StreamingChatChunk chunk in client.StreamChatCompletionAsync(Settings(), []))
        {
            chunks.Add(chunk);
        }

        AiToolCallInfo call = Assert.Single(Assert.Single(chunks).ToolCalls);
        Assert.Equal("call_abc", call.Id);
        Assert.Equal("create_workout_plan", call.Name);
        Assert.Contains("\"name\"", call.ArgumentsJson);
        Assert.Contains("Bench Press", call.ArgumentsJson);
    }

    [Fact]
    public async Task StreamChatCompletionAsync_StopsAtDoneMarker()
    {
        const string sse = """
            data: {"choices":[{"delta":{"content":"first"}}]}

            data: [DONE]

            data: {"choices":[{"delta":{"content":"ignored"}}]}
            """;
        OpenAiCompatibleClient client = CreateClient(_ => SseResponse(sse));

        var chunks = new List<StreamingChatChunk>();
        await foreach (StreamingChatChunk chunk in client.StreamChatCompletionAsync(Settings(), []))
        {
            chunks.Add(chunk);
        }

        Assert.Equal("first", string.Concat(chunks.Select(c => c.DeltaContent)));
    }

    [Fact]
    public async Task StreamChatCompletionAsync_StopsWhenCancelledMidStream()
    {
        const string sse = """
            data: {"choices":[{"delta":{"content":"first"}}]}

            data: {"choices":[{"delta":{"content":"second"}}]}

            data: [DONE]
            """;
        OpenAiCompatibleClient client = CreateClient(_ => SseResponse(sse));
        using var cts = new CancellationTokenSource();

        var chunks = new List<StreamingChatChunk>();
        await foreach (StreamingChatChunk chunk in client.StreamChatCompletionAsync(Settings(), [], cancellationToken: cts.Token))
        {
            chunks.Add(chunk);
            await cts.CancelAsync();
        }

        Assert.Equal("first", string.Concat(chunks.Select(c => c.DeltaContent)));
    }

    [Fact]
    public async Task StreamChatCompletionAsync_HttpError_YieldsErrorChunk()
    {
        OpenAiCompatibleClient client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"error":{"message":"boom"}}""")
        });

        var chunks = new List<StreamingChatChunk>();
        await foreach (StreamingChatChunk chunk in client.StreamChatCompletionAsync(Settings(), []))
        {
            chunks.Add(chunk);
        }

        StreamingChatChunk error = Assert.Single(chunks);
        Assert.True(error.IsError);
        Assert.Contains("500", error.ErrorMessage);
        Assert.Contains("boom", error.ErrorMessage);
    }

    [Fact]
    public async Task SendChatCompletionAsync_ParsesContentAndToolCalls()
    {
        const string responseJson = """
            {"choices":[{"message":{"role":"assistant","content":"I will create the plan","tool_calls":[{"id":"call_1","type":"function","function":{"name":"create_workout_plan","arguments":"{\"name\":\"Push\"}"}}]}}]}
            """;
        OpenAiCompatibleClient client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson)
        });

        OpenAiCompatibleResponse response = await client.SendChatCompletionAsync(Settings(), []);

        Assert.False(response.IsError);
        Assert.Equal("I will create the plan", response.AssistantContent);
        AiToolCallInfo call = Assert.Single(response.ToolCalls);
        Assert.Equal("call_1", call.Id);
        Assert.Equal("create_workout_plan", call.Name);
        Assert.Contains("Push", call.ArgumentsJson);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_Unauthorized_ReturnsEmptyWithoutThrowing()
    {
        OpenAiCompatibleClient client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"error":{"message":"Invalid API key"}}""")
        });

        List<string> models = await client.GetAvailableModelsAsync(Settings());

        Assert.Empty(models);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_ReturnsParsedModelIds()
    {
        OpenAiCompatibleClient client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"data":[{"id":"gpt-4o"},{"id":"gpt-4o-mini"},{"id":""}]}""")
        });

        List<string> models = await client.GetAvailableModelsAsync(Settings());

        Assert.Equal(["gpt-4o", "gpt-4o-mini"], models);
    }
}
