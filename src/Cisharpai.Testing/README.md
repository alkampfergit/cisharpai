# Cisharpai.Testing

Lightweight fake clients for unit testing application code that depends on [Cisharpai](https://www.nuget.org/packages/Cisharpai) interfaces. No real HTTP calls are made.

## Features

- **`FakeChatCompletionClient`** — fake `IChatCompletionClient` with support for `IStreamingChatFeature`, `IToolCallingFeature`, `IJsonOutputFeature`, `IGroundedChatFeature`
- **`FakeEmbeddingClient`** — fake `IEmbeddingClient` with support for `IImageEmbeddingFeature`, `IMultimodalEmbeddingFeature`
- **`FakeRerankerClient`** — fake `IRerankerClient` for reranking
- **`FakeResponses`** — static factory methods for common response objects
- **Response queues** — enqueue specific responses for sequential calls
- **Default responses** — fallback when the queue is empty
- **Request capture** — inspect what your code sent
- **Feature opt-out** — selectively disable features via flags
- **DI helpers** — register fakes in `IServiceCollection` and get the instance back for setup/assertions

## Quick Start

```csharp
using Cisharpai.Testing;
using Cisharpai.Models;

var fake = new FakeChatCompletionClient();
fake.EnqueueResponse(FakeResponses.Chat("Hello from the fake!"));

// Pass 'fake' wherever IChatCompletionClient is expected
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Say hello")]);

var response = await fake.GetChatCompletionAsync(request);

Assert.That(response.Content, Is.EqualTo("Hello from the fake!"));
Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
```

## Response Factories

```csharp
FakeResponses.Chat("content")                     // simple chat response
FakeResponses.ChatError("something went wrong")   // error response
FakeResponses.ToolCall("func", argsJson)           // single tool call
FakeResponses.ToolCalls(toolCall1, toolCall2)       // multiple tool calls
FakeResponses.GroundedChat("content", citations)   // grounded chat with citations
FakeResponses.StreamingChunks("Hello", " world")   // streaming chunks
FakeResponses.Embedding([0.1f, 0.2f])              // embedding response
FakeResponses.EmbeddingError("bad input")          // embedding error
```

## DI Registration

```csharp
using Cisharpai.Testing;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
var fake = services.AddFakeChatCompletionClient();
fake.DefaultResponse = FakeResponses.Chat("default");

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatCompletionClient>();
```

## Feature Opt-Out

Disable specific features to test code paths that check for feature availability:

```csharp
var fake = new FakeChatCompletionClient(
    enabledFeatures: FakeChatFeatures.All & ~FakeChatFeatures.ToolCalling);

// Features.Get<IToolCallingFeature>() will now return null
```

## Links

- [GitHub](https://github.com/alkampfergit/cisharpai)
- [Testing Guide](https://github.com/alkampfergit/cisharpai/blob/main/wiki/testing.md)
