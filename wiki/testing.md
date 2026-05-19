# Testing with Cisharpai

The `Cisharpai.Testing` package provides lightweight fake clients for unit testing application code that depends on Cisharpai interfaces. No real HTTP calls, no API keys, no network required.

## Installation

Add a reference to `Cisharpai.Testing` in your test project:

```xml
<PackageReference Include="Cisharpai.Testing" />
```

Or via project reference:

```xml
<ProjectReference Include="..\Cisharpai.Testing\Cisharpai.Testing.csproj" />
```

## Quick Start

```csharp
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.Testing;

// Create a fake client with a canned response
var fake = new FakeChatCompletionClient
{
    DefaultResponse = FakeResponses.Chat("Paris is the capital of France.")
};

// Use it anywhere IChatCompletionClient is expected
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")]);

var response = await fake.GetChatCompletionAsync(request);

// Assert on the response
Assert.That(response.Content, Is.EqualTo("Paris is the capital of France."));

// Assert on captured requests
Assert.That(fake.CallCount, Is.EqualTo(1));
Assert.That(fake.ReceivedRequests[0].Messages[0].Content, Does.Contain("capital"));
```

## FakeResponses Factory

The `FakeResponses` static class provides convenience methods for creating common response objects with sensible defaults.

### Chat Responses

```csharp
// Simple successful response
FakeResponses.Chat("Hello world");

// With custom model and token counts
FakeResponses.Chat("Hello", model: "gpt-4", promptTokens: 100, completionTokens: 50);

// Error response
FakeResponses.ChatError("rate limit exceeded");
```

### Tool Calling Responses

```csharp
// Single tool call
FakeResponses.ToolCall("get_weather", """{"city":"Paris"}""");

// With a specific tool call ID
FakeResponses.ToolCall("get_weather", """{"city":"Paris"}""", id: "call_123");

// Multiple tool calls
FakeResponses.ToolCalls(
    ("get_weather", """{"city":"Paris"}"""),
    ("get_time", """{"timezone":"UTC"}"""));
```

### Grounded Chat Responses

```csharp
// Without citations
FakeResponses.GroundedChat("The answer is 42.");

// With citations
var citations = new List<Citation>
{
    new(Start: 0, End: 14, Text: "The answer is 42", Sources: [])
};
FakeResponses.GroundedChat("The answer is 42.", citations);
```

### Streaming Responses

```csharp
// Creates chunks from text segments; last chunk has FinishReason = "stop"
FakeResponses.StreamingChunks("Hello", " ", "world");
```

### Embedding Responses

```csharp
// Default vector [0.1, 0.2, 0.3]
FakeResponses.Embedding();

// Custom vector
FakeResponses.Embedding(new float[] { 1.0f, 2.0f, 3.0f });

// Multiple vectors
FakeResponses.Embeddings(new float[][] { [0.1f], [0.2f], [0.3f] });

// Error
FakeResponses.EmbeddingError("model not found");
```

## FakeChatCompletionClient

### Response Configuration

The fake client supports two mechanisms for configuring responses:

1. **Default response** -- returned when the queue is empty (good for tests that always expect the same answer).
2. **Queue** -- responses are dequeued in FIFO order (good for multi-turn conversations or sequential calls).

If neither is configured, the client throws `InvalidOperationException`.

```csharp
var fake = new FakeChatCompletionClient();

// Option 1: Set a default (always returns this)
fake.DefaultResponse = FakeResponses.Chat("default answer");

// Option 2: Enqueue specific responses (consumed in order)
fake.EnqueueResponse(FakeResponses.Chat("first call"));
fake.EnqueueResponse(FakeResponses.Chat("second call"));
// After the queue is drained, falls back to DefaultResponse
```

Each feature method has its own queue and default:

| Method | Queue | Default |
|--------|-------|---------|
| `GetChatCompletionAsync` | `EnqueueResponse()` | `DefaultResponse` |
| `GetChatCompletionWithJsonOutputAsync` | `EnqueueJsonOutputResponse()` | `DefaultJsonOutputResponse` (falls back to `DefaultResponse`) |
| `GetChatCompletionWithToolsAsync` | `EnqueueToolCallingResponse()` | `DefaultToolCallingResponse` |
| `GetGroundedChatCompletionAsync` | `EnqueueGroundedChatResponse()` | `DefaultGroundedChatResponse` |
| `GetChatCompletionStreamAsync` | `EnqueueStreamingResponse()` | `DefaultStreamingResponse` |

### Request Capture

Every call is recorded for later assertions:

```csharp
var fake = new FakeChatCompletionClient
{
    DefaultResponse = FakeResponses.Chat("ok")
};

await fake.GetChatCompletionAsync(request);

// Inspect captured requests
Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
Assert.That(fake.ReceivedRequests[0].Messages[0].Content, Is.EqualTo("expected prompt"));

// CallCount includes ALL methods (chat, tool calling, JSON, grounded, streaming)
Assert.That(fake.CallCount, Is.EqualTo(1));
```

Available capture lists:

| Property | Captures calls from |
|----------|-------------------|
| `ReceivedRequests` | `GetChatCompletionAsync` |
| `ReceivedToolCallingRequests` | `GetChatCompletionWithToolsAsync` (includes `ToolCallingOptions`) |
| `ReceivedJsonOutputRequests` | `GetChatCompletionWithJsonOutputAsync` (includes `JsonOutputOptions`) |
| `ReceivedGroundedChatRequests` | `GetGroundedChatCompletionAsync` (includes `GroundedChatOptions`) |
| `ReceivedStreamingRequests` | `GetChatCompletionStreamAsync` |

### Reset

Clear all queued responses and captured requests between tests:

```csharp
fake.Reset();
```

## FakeEmbeddingClient

Works the same way as the chat client, with queues and defaults for each method.

```csharp
var fake = new FakeEmbeddingClient
{
    DefaultResponse = FakeResponses.Embedding()
};

var result = await fake.GetEmbeddingsAsync(new EmbeddingRequest(["hello"]));
Assert.That(result.Embeddings[0], Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f }));
Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
```

### Image and Multimodal Embedding

```csharp
var fake = new FakeEmbeddingClient
{
    DefaultImageResponse = FakeResponses.Embedding(new float[] { 0.5f, 0.6f }),
    DefaultMultimodalResponse = FakeResponses.Embedding(new float[] { 0.7f, 0.8f })
};

// Image embedding
var imageResult = await fake.GetImageEmbeddingAsync("/path/to/image.png", "model-v1");
Assert.That(fake.ReceivedImageRequests[0].ImagePath, Is.EqualTo("/path/to/image.png"));

// Multimodal embedding
var inputs = new List<MultimodalEmbeddingInput>
{
    new([new TextEmbeddingContent("hello"), new ImageEmbeddingContent("/img.png")])
};
var mmResult = await fake.GetMultimodalEmbeddingsAsync(inputs, "model-v1");
Assert.That(fake.ReceivedMultimodalRequests, Has.Count.EqualTo(1));
```

## Feature Opt-Out

Both fake clients register all feature interfaces by default. Use the flags enums to control which features are available -- useful for testing feature-detection code paths.

### FakeChatFeatures

```csharp
// All features (default)
var full = new FakeChatCompletionClient(FakeChatFeatures.All);

// Only streaming and tool calling
var partial = new FakeChatCompletionClient(
    FakeChatFeatures.Streaming | FakeChatFeatures.ToolCalling);

// No features at all
var bare = new FakeChatCompletionClient(FakeChatFeatures.None);

// Test that your code handles missing features gracefully
var streaming = bare.Features.Get<IStreamingChatFeature>();
Assert.That(streaming, Is.Null); // Feature not available
```

Available flags: `Streaming`, `ToolCalling`, `JsonOutput`, `GroundedChat`, `All`, `None`.

### FakeEmbeddingFeatures

```csharp
// Only image embedding, no multimodal
var imageOnly = new FakeEmbeddingClient(FakeEmbeddingFeatures.ImageEmbedding);
```

Available flags: `ImageEmbedding`, `MultimodalEmbedding`, `All`, `None`.

## Dependency Injection

The DI extensions register the fake as a singleton and return the instance for setup and assertions:

```csharp
using Cisharpai.Testing;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register fake chat client -- returns the instance
var fakeChatClient = services.AddFakeChatCompletionClient();
fakeChatClient.DefaultResponse = FakeResponses.Chat("mocked answer");

// Register fake embedding client
var fakeEmbeddingClient = services.AddFakeEmbeddingClient();
fakeEmbeddingClient.DefaultResponse = FakeResponses.Embedding();

var provider = services.BuildServiceProvider();

// Your service resolves IChatCompletionClient and gets the fake
var myService = provider.GetRequiredService<MyService>();
var result = await myService.DoSomethingAsync();

// Assert on what was sent
Assert.That(fakeChatClient.ReceivedRequests, Has.Count.EqualTo(1));
```

### Selective Features via DI

```csharp
// Register a fake that only exposes streaming
var fake = services.AddFakeChatCompletionClient(FakeChatFeatures.Streaming);
```

## Testing Patterns

### Testing a Service That Uses Chat Completion

```csharp
public class TranslationService(IChatCompletionClient client)
{
    public async Task<string> TranslateAsync(string text, string targetLanguage)
    {
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, $"Translate to {targetLanguage}: {text}")],
            Temperature: 0.1);

        var response = await client.GetChatCompletionAsync(request);
        return response.Content;
    }
}

[Test]
public async Task TranslateAsync_SendsCorrectPrompt()
{
    var fake = new FakeChatCompletionClient
    {
        DefaultResponse = FakeResponses.Chat("Bonjour le monde")
    };
    var service = new TranslationService(fake);

    var result = await service.TranslateAsync("Hello world", "French");

    Assert.That(result, Is.EqualTo("Bonjour le monde"));
    Assert.That(fake.ReceivedRequests[0].Messages[0].Content,
        Does.Contain("French").And.Contain("Hello world"));
    Assert.That(fake.ReceivedRequests[0].Temperature, Is.EqualTo(0.1));
}
```

### Testing Error Handling

```csharp
[Test]
public async Task HandlesApiError_Gracefully()
{
    var fake = new FakeChatCompletionClient
    {
        DefaultResponse = FakeResponses.ChatError("Service unavailable")
    };
    var service = new TranslationService(fake);

    // Your service should handle errors without throwing
    var result = await service.TranslateAsync("Hello", "French");

    Assert.That(result, Is.Empty.Or.EqualTo("Translation failed"));
}
```

### Testing a Multi-Turn Tool Calling Loop

```csharp
[Test]
public async Task ToolCallingLoop_ExecutesToolAndContinues()
{
    var fake = new FakeChatCompletionClient();

    // First call: model requests a tool call
    fake.EnqueueToolCallingResponse(
        FakeResponses.ToolCall("get_weather", """{"city":"Paris"}""", id: "call_1"));

    // Second call: model produces final text
    fake.EnqueueToolCallingResponse(new ToolCallingResponse(
        ChatCompletion: FakeResponses.Chat("It's 22C in Paris."),
        ToolCalls: null));

    var toolFeature = fake.Features.Get<IToolCallingFeature>()!;
    var toolOptions = new ToolCallingOptions(
        Tools: [new ToolDefinition("get_weather", "Get weather",
            JsonDocument.Parse("{}").RootElement)]);

    // First turn
    var response = await toolFeature.GetChatCompletionWithToolsAsync(
        new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Weather in Paris?")]),
        toolOptions);

    Assert.That(response.ToolCalls, Has.Count.EqualTo(1));

    // Simulate tool execution and second turn
    response = await toolFeature.GetChatCompletionWithToolsAsync(
        new ChatCompletionRequest([
            new LlmMessage(LlmRole.User, "Weather in Paris?"),
            new LlmMessage(LlmRole.Assistant, "", ToolCalls: response.ToolCalls),
            new LlmMessage(LlmRole.Tool, """{"temp":"22C"}""", ToolCallId: "call_1")
        ]),
        toolOptions);

    Assert.That(response.ToolCalls, Is.Null);
    Assert.That(response.Content, Is.EqualTo("It's 22C in Paris."));
    Assert.That(fake.ReceivedToolCallingRequests, Has.Count.EqualTo(2));
}
```

### Testing Streaming Consumption

```csharp
[Test]
public async Task StreamingAccumulation_CollectsAllChunks()
{
    var fake = new FakeChatCompletionClient
    {
        DefaultStreamingResponse = FakeResponses.StreamingChunks("Hello", " ", "world", "!")
    };

    var streamFeature = fake.Features.Get<IStreamingChatFeature>()!;
    var accumulated = new System.Text.StringBuilder();

    await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(
        new ChatCompletionRequest([new LlmMessage(LlmRole.User, "greet me")])))
    {
        accumulated.Append(chunk.Content);
    }

    Assert.That(accumulated.ToString(), Is.EqualTo("Hello world!"));
    Assert.That(fake.ReceivedStreamingRequests, Has.Count.EqualTo(1));
}
```

### Testing Feature Detection

```csharp
[Test]
public void Service_FallsBackWhenStreamingUnavailable()
{
    // Client without streaming
    var fake = new FakeChatCompletionClient(FakeChatFeatures.None)
    {
        DefaultResponse = FakeResponses.Chat("non-streamed response")
    };

    var streaming = fake.Features.Get<IStreamingChatFeature>();
    if (streaming is null)
    {
        // Your service should fall back to non-streaming
        var response = fake.GetChatCompletionAsync(
            new ChatCompletionRequest([new LlmMessage(LlmRole.User, "hi")])).Result;

        Assert.That(response.Content, Is.EqualTo("non-streamed response"));
    }
}
```

### Testing Sequential Responses

```csharp
[Test]
public async Task ConversationAgent_HandlesMultipleTurns()
{
    var fake = new FakeChatCompletionClient();
    fake.EnqueueResponse(FakeResponses.Chat("I can help with that."));
    fake.EnqueueResponse(FakeResponses.Chat("Here's the answer: 42."));
    fake.EnqueueResponse(FakeResponses.Chat("You're welcome!"));

    var request = new ChatCompletionRequest(
        [new LlmMessage(LlmRole.User, "help")]);

    var r1 = await fake.GetChatCompletionAsync(request);
    var r2 = await fake.GetChatCompletionAsync(request);
    var r3 = await fake.GetChatCompletionAsync(request);

    Assert.That(r1.Content, Is.EqualTo("I can help with that."));
    Assert.That(r2.Content, Is.EqualTo("Here's the answer: 42."));
    Assert.That(r3.Content, Is.EqualTo("You're welcome!"));
}
```

## API Reference

### FakeChatCompletionClient

| Member | Type | Description |
|--------|------|-------------|
| `DefaultResponse` | `ChatCompletionResponse?` | Fallback for `GetChatCompletionAsync` and `GetChatCompletionWithJsonOutputAsync` |
| `DefaultJsonOutputResponse` | `ChatCompletionResponse?` | Fallback for `GetChatCompletionWithJsonOutputAsync` (overrides `DefaultResponse`) |
| `DefaultToolCallingResponse` | `ToolCallingResponse?` | Fallback for `GetChatCompletionWithToolsAsync` |
| `DefaultGroundedChatResponse` | `GroundedChatCompletionResponse?` | Fallback for `GetGroundedChatCompletionAsync` |
| `DefaultStreamingResponse` | `IReadOnlyList<ChatCompletionChunk>?` | Fallback for `GetChatCompletionStreamAsync` |
| `CallCount` | `int` | Total calls across all methods |
| `ReceivedRequests` | `IReadOnlyList<ChatCompletionRequest>` | Captured chat requests |
| `ReceivedToolCallingRequests` | `IReadOnlyList<(Request, Options)>` | Captured tool calling requests |
| `ReceivedJsonOutputRequests` | `IReadOnlyList<(Request, Options)>` | Captured JSON output requests |
| `ReceivedGroundedChatRequests` | `IReadOnlyList<(Request, Options)>` | Captured grounded chat requests |
| `ReceivedStreamingRequests` | `IReadOnlyList<ChatCompletionRequest>` | Captured streaming requests |
| `Reset()` | `void` | Clears all queues and captured requests |

### FakeEmbeddingClient

| Member | Type | Description |
|--------|------|-------------|
| `DefaultResponse` | `EmbeddingResponse?` | Fallback for `GetEmbeddingsAsync` and feature methods |
| `DefaultImageResponse` | `EmbeddingResponse?` | Fallback for `GetImageEmbeddingAsync` |
| `DefaultMultimodalResponse` | `EmbeddingResponse?` | Fallback for `GetMultimodalEmbeddingsAsync` |
| `CallCount` | `int` | Total calls across all methods |
| `ReceivedRequests` | `IReadOnlyList<EmbeddingRequest>` | Captured embedding requests |
| `ReceivedImageRequests` | `IReadOnlyList<(ImagePath, Model)>` | Captured image embedding requests |
| `ReceivedMultimodalRequests` | `IReadOnlyList<IReadOnlyList<MultimodalEmbeddingInput>>` | Captured multimodal requests |
| `Reset()` | `void` | Clears all queues and captured requests |

### FakeClientFactoryProvider

A fake `IClientFactoryProvider` for testing code that depends on `ICisharpaiClientFactory`. By default it registers as the OpenAI provider; pass a different `CisharpaiProvider` to the constructor to fake any provider.

```csharp
var fakeProvider = new FakeClientFactoryProvider()
    .WithDefaultChatClient(new FakeChatCompletionClient
    {
        DefaultResponse = FakeResponses.Chat("Hello!")
    });

var services = new ServiceCollection();
services.AddCisharpaiClientFactory()
    .AddFakeSupport(fakeProvider);

using var sp = services.BuildServiceProvider();
var factory = sp.GetRequiredService<ICisharpaiClientFactory>();

var config = new OpenAiClientConfiguration { ApiKey = "fake" };
var result = factory.CreateChatCompletionClient(config);
Assert.That(result.IsSuccess, Is.True);
```

To fake a different provider:

```csharp
var fakeAnthropic = new FakeClientFactoryProvider(CisharpaiProvider.Anthropic)
    .WithDefaultChatClient(myFakeClient);
services.AddCisharpaiClientFactory()
    .AddFakeSupport(fakeAnthropic);
```

| Member | Type | Description |
|--------|------|-------------|
| `EnqueueChatClient(client)` | `FakeClientFactoryProvider` | Queue a chat client (FIFO) |
| `EnqueueEmbeddingClient(client)` | `FakeClientFactoryProvider` | Queue an embedding client (FIFO) |
| `WithDefaultChatClient(client)` | `FakeClientFactoryProvider` | Set default chat client (used when queue empty) |
| `WithDefaultEmbeddingClient(client)` | `FakeClientFactoryProvider` | Set default embedding client (used when queue empty) |
