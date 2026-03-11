using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Testing;

[TestFixture]
public class FakeChatCompletionClientTests
{
    [Test]
    public async Task GetChatCompletionAsync_ReturnsDefaultResponse()
    {
        var fake = new FakeChatCompletionClient
        {
            DefaultResponse = FakeResponses.Chat("Hello!")
        };

        var result = await fake.GetChatCompletionAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")]));

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Is.EqualTo("Hello!"));
            Assert.That(result.IsSuccess, Is.True);
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_ReturnsQueuedResponseFirst()
    {
        var fake = new FakeChatCompletionClient
        {
            DefaultResponse = FakeResponses.Chat("default")
        };
        fake.EnqueueResponse(FakeResponses.Chat("queued"));

        var first = await fake.GetChatCompletionAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")]));
        var second = await fake.GetChatCompletionAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")]));

        Assert.Multiple(() =>
        {
            Assert.That(first.Content, Is.EqualTo("queued"));
            Assert.That(second.Content, Is.EqualTo("default"));
        });
    }

    [Test]
    public void GetChatCompletionAsync_ThrowsWhenNoResponseConfigured()
    {
        var fake = new FakeChatCompletionClient();

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            fake.GetChatCompletionAsync(
                new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Hi")])));
    }

    [Test]
    public async Task CapturesReceivedRequests()
    {
        var fake = new FakeChatCompletionClient
        {
            DefaultResponse = FakeResponses.Chat("ok")
        };

        var request = new ChatCompletionRequest([new LlmMessage(LlmRole.User, "test")]);
        await fake.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
            Assert.That(fake.ReceivedRequests[0].Messages[0].Content, Is.EqualTo("test"));
        });
    }

    [Test]
    public async Task CallCount_TracksAllMethods()
    {
        var fake = new FakeChatCompletionClient
        {
            DefaultResponse = FakeResponses.Chat("ok"),
            DefaultToolCallingResponse = FakeResponses.ToolCall("fn", "{}"),
            DefaultStreamingResponse = FakeResponses.StreamingChunks("hi")
        };

        var request = new ChatCompletionRequest([new LlmMessage(LlmRole.User, "test")]);

        await fake.GetChatCompletionAsync(request);
        await fake.GetChatCompletionWithToolsAsync(request,
            new ToolCallingOptions([new ToolDefinition("fn", "desc", System.Text.Json.JsonDocument.Parse("{}").RootElement)]));
        await foreach (var _ in fake.GetChatCompletionStreamAsync(request)) { }

        Assert.That(fake.CallCount, Is.EqualTo(3));
    }

    [Test]
    public async Task ToolCalling_CapturesRequestAndOptions()
    {
        var fake = new FakeChatCompletionClient
        {
            DefaultToolCallingResponse = FakeResponses.ToolCall("get_weather", """{"city":"Paris"}""")
        };

        var request = new ChatCompletionRequest([new LlmMessage(LlmRole.User, "weather?")]);
        var options = new ToolCallingOptions(
            [new ToolDefinition("get_weather", "Gets weather", System.Text.Json.JsonDocument.Parse("{}").RootElement)]);

        var result = await fake.GetChatCompletionWithToolsAsync(request, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.ToolCalls, Has.Count.EqualTo(1));
            Assert.That(result.ToolCalls![0].FunctionName, Is.EqualTo("get_weather"));
            Assert.That(fake.ReceivedToolCallingRequests, Has.Count.EqualTo(1));
            Assert.That(fake.ReceivedToolCallingRequests[0].Options.Tools, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task JsonOutput_CapturesRequestAndOptions()
    {
        var fake = new FakeChatCompletionClient
        {
            DefaultJsonOutputResponse = FakeResponses.Chat("""{"answer":"Paris"}""")
        };

        var request = new ChatCompletionRequest([new LlmMessage(LlmRole.User, "capital?")]);
        var options = new JsonOutputOptions(JsonOutputMode.JsonMode);

        var result = await fake.GetChatCompletionWithJsonOutputAsync(request, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Is.EqualTo("""{"answer":"Paris"}"""));
            Assert.That(fake.ReceivedJsonOutputRequests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task JsonOutput_FallsBackToDefaultResponse()
    {
        var fake = new FakeChatCompletionClient
        {
            DefaultResponse = FakeResponses.Chat("fallback")
        };

        var result = await fake.GetChatCompletionWithJsonOutputAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "hi")]),
            new JsonOutputOptions(JsonOutputMode.JsonMode));

        Assert.That(result.Content, Is.EqualTo("fallback"));
    }

    [Test]
    public async Task GroundedChat_CapturesRequestAndOptions()
    {
        var fake = new FakeChatCompletionClient
        {
            DefaultGroundedChatResponse = FakeResponses.GroundedChat("grounded answer")
        };

        var request = new ChatCompletionRequest([new LlmMessage(LlmRole.User, "question")]);
        var options = new GroundedChatOptions([new DocumentChunk("doc1", Text: "some text")]);

        var result = await fake.GetGroundedChatCompletionAsync(request, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Is.EqualTo("grounded answer"));
            Assert.That(fake.ReceivedGroundedChatRequests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Streaming_ReturnsChunksInOrder()
    {
        var fake = new FakeChatCompletionClient
        {
            DefaultStreamingResponse = FakeResponses.StreamingChunks("Hello", " world")
        };

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in fake.GetChatCompletionStreamAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "hi")])))
        {
            chunks.Add(chunk);
        }

        Assert.Multiple(() =>
        {
            Assert.That(chunks, Has.Count.EqualTo(2));
            Assert.That(string.Concat(chunks.Select(c => c.Content)), Is.EqualTo("Hello world"));
            Assert.That(chunks[0].FinishReason, Is.Null);
            Assert.That(chunks[1].FinishReason, Is.EqualTo("stop"));
        });
    }

    [Test]
    public async Task Streaming_SupportsQueuedResponses()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueStreamingResponse(FakeResponses.StreamingChunks("first"));
        fake.EnqueueStreamingResponse(FakeResponses.StreamingChunks("second"));

        var request = new ChatCompletionRequest([new LlmMessage(LlmRole.User, "hi")]);

        var first = new List<string>();
        await foreach (var c in fake.GetChatCompletionStreamAsync(request))
            first.Add(c.Content);

        var second = new List<string>();
        await foreach (var c in fake.GetChatCompletionStreamAsync(request))
            second.Add(c.Content);

        Assert.Multiple(() =>
        {
            Assert.That(string.Join("", first), Is.EqualTo("first"));
            Assert.That(string.Join("", second), Is.EqualTo("second"));
        });
    }

    [Test]
    public void Features_AllRegisteredByDefault()
    {
        var fake = new FakeChatCompletionClient();

        Assert.Multiple(() =>
        {
            Assert.That(fake.Features.Get<IStreamingChatFeature>(), Is.Not.Null);
            Assert.That(fake.Features.Get<IToolCallingFeature>(), Is.Not.Null);
            Assert.That(fake.Features.Get<IJsonOutputFeature>(), Is.Not.Null);
            Assert.That(fake.Features.Get<IGroundedChatFeature>(), Is.Not.Null);
        });
    }

    [Test]
    public void Features_CanBeSelective()
    {
        var fake = new FakeChatCompletionClient(FakeChatFeatures.Streaming | FakeChatFeatures.ToolCalling);

        Assert.Multiple(() =>
        {
            Assert.That(fake.Features.Get<IStreamingChatFeature>(), Is.Not.Null);
            Assert.That(fake.Features.Get<IToolCallingFeature>(), Is.Not.Null);
            Assert.That(fake.Features.Get<IJsonOutputFeature>(), Is.Null);
            Assert.That(fake.Features.Get<IGroundedChatFeature>(), Is.Null);
        });
    }

    [Test]
    public void Features_None()
    {
        var fake = new FakeChatCompletionClient(FakeChatFeatures.None);

        Assert.Multiple(() =>
        {
            Assert.That(fake.Features.Get<IStreamingChatFeature>(), Is.Null);
            Assert.That(fake.Features.Get<IToolCallingFeature>(), Is.Null);
            Assert.That(fake.Features.Get<IJsonOutputFeature>(), Is.Null);
            Assert.That(fake.Features.Get<IGroundedChatFeature>(), Is.Null);
        });
    }

    [Test]
    public async Task Reset_ClearsEverything()
    {
        var fake = new FakeChatCompletionClient
        {
            DefaultResponse = FakeResponses.Chat("ok")
        };
        fake.EnqueueResponse(FakeResponses.Chat("queued"));
        await fake.GetChatCompletionAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "test")]));

        fake.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(fake.ReceivedRequests, Is.Empty);
            Assert.That(fake.CallCount, Is.EqualTo(0));
        });
        // Queue was cleared, so default should be used
        var result = await fake.GetChatCompletionAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "test")]));
        Assert.That(result.Content, Is.EqualTo("ok"));
    }

    [Test]
    public async Task MultipleQueuedResponses_DrainInOrder()
    {
        var fake = new FakeChatCompletionClient();
        fake.EnqueueResponse(FakeResponses.Chat("first"));
        fake.EnqueueResponse(FakeResponses.Chat("second"));
        fake.EnqueueResponse(FakeResponses.Chat("third"));

        var request = new ChatCompletionRequest([new LlmMessage(LlmRole.User, "test")]);
        var r1 = await fake.GetChatCompletionAsync(request);
        var r2 = await fake.GetChatCompletionAsync(request);
        var r3 = await fake.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(r1.Content, Is.EqualTo("first"));
            Assert.That(r2.Content, Is.EqualTo("second"));
            Assert.That(r3.Content, Is.EqualTo("third"));
        });
    }
}
