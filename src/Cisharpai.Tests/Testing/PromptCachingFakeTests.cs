using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Testing;

public sealed class PromptCachingFakeTests
{
    private static readonly int[] ExpectedBreakpoints = [0, 2];
    private static readonly int[] SingleMessageBreakpoint = [1];
    private static readonly int[] SingleToolBreakpoint = [0];

    [Test]
    public void FakeChatCompletionClient_RegistersPromptCachingFeature_ByDefault()
    {
        var client = new FakeChatCompletionClient();
        var feature = client.Features.Get<IPromptCachingFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    [Test]
    public void FakeChatCompletionClient_DoesNotRegisterPromptCachingFeature_WhenExcluded()
    {
        var client = new FakeChatCompletionClient(
            FakeChatFeatures.Streaming | FakeChatFeatures.ToolCalling);
        var feature = client.Features.Get<IPromptCachingFeature>();

        Assert.That(feature, Is.Null);
    }

    [Test]
    public async Task FakeChatCompletionClient_PromptCaching_ReturnsQueuedResponse()
    {
        var client = new FakeChatCompletionClient();
        var expectedResponse = FakeResponses.CachedChat("Hello", cachedInputTokens: 500, cacheCreationInputTokens: 200);
        client.EnqueuePromptCachingResponse(expectedResponse);

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var response = await feature.GetChatCompletionWithCachingAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Hi")],
                Model: "test"),
            new PromptCachingOptions { CacheSystemMessage = true });

        Assert.Multiple(() =>
        {
            Assert.That(response.Content, Is.EqualTo("Hello"));
            Assert.That(response.CachedInputTokens, Is.EqualTo(500));
            Assert.That(response.CacheCreationInputTokens, Is.EqualTo(200));
        });
    }

    [Test]
    public async Task FakeChatCompletionClient_PromptCaching_CapturesRequests()
    {
        var client = new FakeChatCompletionClient();
        client.DefaultPromptCachingResponse = FakeResponses.CachedChat("cached", cachedInputTokens: 100);

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var cachingOptions = new PromptCachingOptions
        {
            CacheSystemMessage = true,
            MessageBreakpoints = [0, 2]
        };

        await feature.GetChatCompletionWithCachingAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Hi")],
                Model: "test"),
            cachingOptions);

        Assert.That(client.ReceivedPromptCachingRequests, Has.Count.EqualTo(1));
        Assert.That(client.ReceivedPromptCachingRequests[0].Options.CacheSystemMessage, Is.True);
        Assert.That(client.ReceivedPromptCachingRequests[0].Options.MessageBreakpoints, Is.EqualTo(ExpectedBreakpoints));
    }

    [Test]
    public void FakeResponses_CachedChat_SetsFields()
    {
        var response = FakeResponses.CachedChat("test", cachedInputTokens: 300, cacheCreationInputTokens: 150);

        Assert.Multiple(() =>
        {
            Assert.That(response.Content, Is.EqualTo("test"));
            Assert.That(response.CachedInputTokens, Is.EqualTo(300));
            Assert.That(response.CacheCreationInputTokens, Is.EqualTo(150));
            Assert.That(response.IsSuccess, Is.True);
        });
    }

    [Test]
    public void FakeResponses_CachedChat_WithoutCreation_HasNullCreation()
    {
        var response = FakeResponses.CachedChat("test", cachedInputTokens: 300);

        Assert.Multiple(() =>
        {
            Assert.That(response.CachedInputTokens, Is.EqualTo(300));
            Assert.That(response.CacheCreationInputTokens, Is.Null);
        });
    }

    [Test]
    public async Task FakeChatCompletionClient_Reset_ClearsPromptCachingState()
    {
        var client = new FakeChatCompletionClient();
        client.EnqueuePromptCachingResponse(FakeResponses.CachedChat("cached", cachedInputTokens: 100));

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        await feature.GetChatCompletionWithCachingAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Hi")],
                Model: "test"),
            new PromptCachingOptions());

        client.Reset();

        Assert.That(client.ReceivedPromptCachingRequests, Is.Empty);
        Assert.That(client.CallCount, Is.EqualTo(0));
    }

    // --- GetGroundedChatCompletionWithCachingAsync tests ---

    [Test]
    public async Task GroundedCaching_ReturnsQueuedResponse()
    {
        var client = new FakeChatCompletionClient();
        var expected = new GroundedChatCompletionResponse(
            ChatCompletion: FakeResponses.CachedChat("grounded cached", cachedInputTokens: 800),
            Citations: []);
        client.EnqueueGroundedCachingResponse(expected);

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var result = await feature.GetGroundedChatCompletionWithCachingAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")], Model: "test"),
            new GroundedChatOptions([new DocumentChunk("doc1", Text: "content")]),
            new PromptCachingOptions { CacheSystemMessage = true });

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Is.EqualTo("grounded cached"));
            Assert.That(result.ChatCompletion.CachedInputTokens, Is.EqualTo(800));
        });
    }

    [Test]
    public async Task GroundedCaching_QueueDrainsInOrder_ThenFallsBackToDefault()
    {
        var client = new FakeChatCompletionClient();
        client.EnqueueGroundedCachingResponse(new GroundedChatCompletionResponse(
            ChatCompletion: FakeResponses.CachedChat("first", cachedInputTokens: 100), Citations: []));
        client.EnqueueGroundedCachingResponse(new GroundedChatCompletionResponse(
            ChatCompletion: FakeResponses.CachedChat("second", cachedInputTokens: 200), Citations: []));
        client.DefaultGroundedCachingResponse = new GroundedChatCompletionResponse(
            ChatCompletion: FakeResponses.CachedChat("default", cachedInputTokens: 50), Citations: []);

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var request = new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")], Model: "test");
        var grounded = new GroundedChatOptions([new DocumentChunk("d", Text: "t")]);
        var caching = new PromptCachingOptions();

        var r1 = await feature.GetGroundedChatCompletionWithCachingAsync(request, grounded, caching);
        var r2 = await feature.GetGroundedChatCompletionWithCachingAsync(request, grounded, caching);
        var r3 = await feature.GetGroundedChatCompletionWithCachingAsync(request, grounded, caching);

        Assert.Multiple(() =>
        {
            Assert.That(r1.Content, Is.EqualTo("first"));
            Assert.That(r2.Content, Is.EqualTo("second"));
            Assert.That(r3.Content, Is.EqualTo("default"));
        });
    }

    [Test]
    public async Task GroundedCaching_FallsBackToDefaultGroundedChatResponse()
    {
        var client = new FakeChatCompletionClient
        {
            DefaultGroundedChatResponse = FakeResponses.GroundedChat("grounded fallback")
        };

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var result = await feature.GetGroundedChatCompletionWithCachingAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")], Model: "test"),
            new GroundedChatOptions([new DocumentChunk("d", Text: "t")]),
            new PromptCachingOptions());

        Assert.That(result.Content, Is.EqualTo("grounded fallback"));
    }

    [Test]
    public void GroundedCaching_ThrowsWhenNoResponseConfigured()
    {
        var client = new FakeChatCompletionClient();
        var feature = client.Features.Get<IPromptCachingFeature>()!;

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            feature.GetGroundedChatCompletionWithCachingAsync(
                new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")], Model: "test"),
                new GroundedChatOptions([new DocumentChunk("d", Text: "t")]),
                new PromptCachingOptions()));
    }

    [Test]
    public async Task GroundedCaching_CapturesAllParameters()
    {
        var client = new FakeChatCompletionClient
        {
            DefaultGroundedCachingResponse = new GroundedChatCompletionResponse(
                ChatCompletion: FakeResponses.CachedChat("ok", cachedInputTokens: 100), Citations: [])
        };

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var groundedOptions = new GroundedChatOptions([new DocumentChunk("doc1", Text: "corpus")]);
        var cachingOptions = new PromptCachingOptions { CacheSystemMessage = true, MessageBreakpoints = [1] };

        await feature.GetGroundedChatCompletionWithCachingAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "q")], Model: "test"),
            groundedOptions,
            cachingOptions);

        Assert.Multiple(() =>
        {
            Assert.That(client.ReceivedGroundedCachingRequests, Has.Count.EqualTo(1));
            Assert.That(client.ReceivedGroundedCachingRequests[0].GroundedOptions.Documents, Has.Count.EqualTo(1));
            Assert.That(client.ReceivedGroundedCachingRequests[0].GroundedOptions.Documents[0].Id, Is.EqualTo("doc1"));
            Assert.That(client.ReceivedGroundedCachingRequests[0].CachingOptions.CacheSystemMessage, Is.True);
            Assert.That(client.ReceivedGroundedCachingRequests[0].CachingOptions.MessageBreakpoints, Is.EqualTo(SingleMessageBreakpoint));
            Assert.That(client.ReceivedGroundedCachingRequests[0].Request.Messages[0].Content, Is.EqualTo("q"));
        });
    }

    [Test]
    public async Task GroundedCaching_Reset_ClearsQueueAndCapture()
    {
        var client = new FakeChatCompletionClient();
        client.EnqueueGroundedCachingResponse(new GroundedChatCompletionResponse(
            ChatCompletion: FakeResponses.CachedChat("cached", cachedInputTokens: 100), Citations: []));

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        await feature.GetGroundedChatCompletionWithCachingAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")], Model: "test"),
            new GroundedChatOptions([new DocumentChunk("d", Text: "t")]),
            new PromptCachingOptions());

        client.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(client.ReceivedGroundedCachingRequests, Is.Empty);
            Assert.That(client.CallCount, Is.EqualTo(0));
        });
    }

    // --- GetChatCompletionWithToolsAndCachingAsync tests ---

    [Test]
    public async Task ToolCaching_ReturnsQueuedResponse()
    {
        var client = new FakeChatCompletionClient();
        var expected = FakeResponses.ToolCall("get_weather", """{"city":"Rome"}""");
        client.EnqueueToolCachingResponse(expected);

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var result = await feature.GetChatCompletionWithToolsAndCachingAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "weather")], Model: "test"),
            new ToolCallingOptions([new ToolDefinition("get_weather", "Gets weather",
                System.Text.Json.JsonDocument.Parse("{}").RootElement)]),
            new PromptCachingOptions { ToolBreakpoints = [0] });

        Assert.Multiple(() =>
        {
            Assert.That(result.ToolCalls, Has.Count.EqualTo(1));
            Assert.That(result.ToolCalls![0].FunctionName, Is.EqualTo("get_weather"));
        });
    }

    [Test]
    public async Task ToolCaching_QueueDrainsInOrder_ThenFallsBackToDefault()
    {
        var client = new FakeChatCompletionClient();
        client.EnqueueToolCachingResponse(FakeResponses.ToolCall("fn_a", "{}"));
        client.EnqueueToolCachingResponse(FakeResponses.ToolCall("fn_b", "{}"));
        client.DefaultToolCachingResponse = FakeResponses.ToolCall("fn_default", "{}");

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var request = new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")], Model: "test");
        var toolOpts = new ToolCallingOptions([new ToolDefinition("fn", "d",
            System.Text.Json.JsonDocument.Parse("{}").RootElement)]);
        var caching = new PromptCachingOptions();

        var r1 = await feature.GetChatCompletionWithToolsAndCachingAsync(request, toolOpts, caching);
        var r2 = await feature.GetChatCompletionWithToolsAndCachingAsync(request, toolOpts, caching);
        var r3 = await feature.GetChatCompletionWithToolsAndCachingAsync(request, toolOpts, caching);

        Assert.Multiple(() =>
        {
            Assert.That(r1.ToolCalls![0].FunctionName, Is.EqualTo("fn_a"));
            Assert.That(r2.ToolCalls![0].FunctionName, Is.EqualTo("fn_b"));
            Assert.That(r3.ToolCalls![0].FunctionName, Is.EqualTo("fn_default"));
        });
    }

    [Test]
    public async Task ToolCaching_FallsBackToDefaultToolCallingResponse()
    {
        var client = new FakeChatCompletionClient
        {
            DefaultToolCallingResponse = FakeResponses.ToolCall("fallback_fn", "{}")
        };

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var result = await feature.GetChatCompletionWithToolsAndCachingAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")], Model: "test"),
            new ToolCallingOptions([new ToolDefinition("fn", "d",
                System.Text.Json.JsonDocument.Parse("{}").RootElement)]),
            new PromptCachingOptions());

        Assert.That(result.ToolCalls![0].FunctionName, Is.EqualTo("fallback_fn"));
    }

    [Test]
    public void ToolCaching_ThrowsWhenNoResponseConfigured()
    {
        var client = new FakeChatCompletionClient();
        var feature = client.Features.Get<IPromptCachingFeature>()!;

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            feature.GetChatCompletionWithToolsAndCachingAsync(
                new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")], Model: "test"),
                new ToolCallingOptions([new ToolDefinition("fn", "d",
                    System.Text.Json.JsonDocument.Parse("{}").RootElement)]),
                new PromptCachingOptions()));
    }

    [Test]
    public async Task ToolCaching_CapturesAllParameters()
    {
        var client = new FakeChatCompletionClient
        {
            DefaultToolCachingResponse = FakeResponses.ToolCall("fn", "{}")
        };

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var toolOpts = new ToolCallingOptions([new ToolDefinition("calc", "Calculator",
            System.Text.Json.JsonDocument.Parse("""{"type":"object"}""").RootElement)]);
        var cachingOpts = new PromptCachingOptions { ToolBreakpoints = [0], CacheSystemMessage = true };

        await feature.GetChatCompletionWithToolsAndCachingAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "compute")], Model: "test"),
            toolOpts,
            cachingOpts);

        Assert.Multiple(() =>
        {
            Assert.That(client.ReceivedToolCachingRequests, Has.Count.EqualTo(1));
            Assert.That(client.ReceivedToolCachingRequests[0].ToolOptions.Tools, Has.Count.EqualTo(1));
            Assert.That(client.ReceivedToolCachingRequests[0].ToolOptions.Tools[0].Name, Is.EqualTo("calc"));
            Assert.That(client.ReceivedToolCachingRequests[0].CachingOptions.ToolBreakpoints, Is.EqualTo(SingleToolBreakpoint));
            Assert.That(client.ReceivedToolCachingRequests[0].CachingOptions.CacheSystemMessage, Is.True);
            Assert.That(client.ReceivedToolCachingRequests[0].Request.Messages[0].Content, Is.EqualTo("compute"));
        });
    }

    [Test]
    public async Task ToolCaching_Reset_ClearsQueueAndCapture()
    {
        var client = new FakeChatCompletionClient();
        client.EnqueueToolCachingResponse(FakeResponses.ToolCall("fn", "{}"));

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        await feature.GetChatCompletionWithToolsAndCachingAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")], Model: "test"),
            new ToolCallingOptions([new ToolDefinition("fn", "d",
                System.Text.Json.JsonDocument.Parse("{}").RootElement)]),
            new PromptCachingOptions());

        client.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(client.ReceivedToolCachingRequests, Is.Empty);
            Assert.That(client.CallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GroundedAndToolCaching_ContributeToCallCount()
    {
        var client = new FakeChatCompletionClient
        {
            DefaultGroundedCachingResponse = new GroundedChatCompletionResponse(
                ChatCompletion: FakeResponses.CachedChat("g", cachedInputTokens: 10), Citations: []),
            DefaultToolCachingResponse = FakeResponses.ToolCall("fn", "{}"),
            DefaultPromptCachingResponse = FakeResponses.CachedChat("c", cachedInputTokens: 10)
        };

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var request = new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")], Model: "test");

        await feature.GetChatCompletionWithCachingAsync(request, new PromptCachingOptions());
        await feature.GetGroundedChatCompletionWithCachingAsync(request,
            new GroundedChatOptions([new DocumentChunk("d", Text: "t")]), new PromptCachingOptions());
        await feature.GetChatCompletionWithToolsAndCachingAsync(request,
            new ToolCallingOptions([new ToolDefinition("fn", "d",
                System.Text.Json.JsonDocument.Parse("{}").RootElement)]),
            new PromptCachingOptions());

        Assert.That(client.CallCount, Is.EqualTo(3));
    }
}
