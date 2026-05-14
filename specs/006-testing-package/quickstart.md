# Quickstart: Testing Package

## Prerequisites

- .NET 8.0 or .NET 10 SDK
- A test framework (NUnit, xUnit, MSTest, etc.)

## Install

```xml
<PackageReference Include="Cisharpai.Testing" />
```

## 1. Basic Chat Completion Fake

```csharp
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.Testing;

var fake = new FakeChatCompletionClient
{
    DefaultResponse = FakeResponses.Chat("Paris is the capital of France.")
};

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")]);

var response = await fake.GetChatCompletionAsync(request);

Assert.That(response.Content, Is.EqualTo("Paris is the capital of France."));
Assert.That(fake.ReceivedRequests[0].Messages[0].Content, Does.Contain("capital"));
```

## 2. Sequential Responses (Multi-Turn)

```csharp
var fake = new FakeChatCompletionClient();
fake.EnqueueResponse(FakeResponses.Chat("first"));
fake.EnqueueResponse(FakeResponses.Chat("second"));
fake.EnqueueResponse(FakeResponses.Chat("third"));

var request = new ChatCompletionRequest([new LlmMessage(LlmRole.User, "test")]);
var r1 = await fake.GetChatCompletionAsync(request); // "first"
var r2 = await fake.GetChatCompletionAsync(request); // "second"
var r3 = await fake.GetChatCompletionAsync(request); // "third"
```

## 3. Tool Calling

```csharp
var fake = new FakeChatCompletionClient
{
    DefaultToolCallingResponse = FakeResponses.ToolCall("get_weather", """{"city":"Paris"}""")
};

var toolFeature = fake.Features.Get<IToolCallingFeature>()!;
var options = new ToolCallingOptions(
    Tools: [new ToolDefinition("get_weather", "Gets weather",
        JsonDocument.Parse("{}").RootElement)]);

var result = await toolFeature.GetChatCompletionWithToolsAsync(
    new ChatCompletionRequest([new LlmMessage(LlmRole.User, "Weather?")]),
    options);

Assert.That(result.ToolCalls![0].FunctionName, Is.EqualTo("get_weather"));
Assert.That(fake.ReceivedToolCallingRequests, Has.Count.EqualTo(1));
```

## 4. Streaming

```csharp
var fake = new FakeChatCompletionClient
{
    DefaultStreamingResponse = FakeResponses.StreamingChunks("Hello", " ", "world")
};

var streamFeature = fake.Features.Get<IStreamingChatFeature>()!;
var accumulated = new StringBuilder();

await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(
    new ChatCompletionRequest([new LlmMessage(LlmRole.User, "greet me")])))
{
    accumulated.Append(chunk.Content);
}

Assert.That(accumulated.ToString(), Is.EqualTo("Hello world"));
```

## 5. Error Handling

```csharp
var fake = new FakeChatCompletionClient
{
    DefaultResponse = FakeResponses.ChatError("rate limit exceeded")
};

var response = await fake.GetChatCompletionAsync(
    new ChatCompletionRequest([new LlmMessage(LlmRole.User, "hi")]));

Assert.That(response.IsSuccess, Is.False);
Assert.That(response.ErrorMessage, Is.EqualTo("rate limit exceeded"));
```

## 6. Embedding Fake

```csharp
var fake = new FakeEmbeddingClient
{
    DefaultResponse = FakeResponses.Embedding()
};

var result = await fake.GetEmbeddingsAsync(new EmbeddingRequest(["hello"]));

Assert.That(result.Embeddings[0], Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f }));
Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
```

## 7. Feature Opt-Out

```csharp
// Only streaming — no tool calling, no JSON output, no grounded chat
var fake = new FakeChatCompletionClient(
    FakeChatFeatures.Streaming | FakeChatFeatures.ToolCalling);

Assert.That(fake.Features.Get<IStreamingChatFeature>(), Is.Not.Null);
Assert.That(fake.Features.Get<IJsonOutputFeature>(), Is.Null);

// No features at all
var bare = new FakeChatCompletionClient(FakeChatFeatures.None);
Assert.That(bare.Features.Get<IStreamingChatFeature>(), Is.Null);
```

## 8. DI Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
var fakeChatClient = services.AddFakeChatCompletionClient();
fakeChatClient.DefaultResponse = FakeResponses.Chat("mocked answer");

var fakeEmbeddingClient = services.AddFakeEmbeddingClient();
fakeEmbeddingClient.DefaultResponse = FakeResponses.Embedding();

var provider = services.BuildServiceProvider();

// Resolve via interface — gets the fake
var chatClient = provider.GetRequiredService<IChatCompletionClient>();
var embeddingClient = provider.GetRequiredService<IEmbeddingClient>();
```

## Verification

```bash
dotnet test src/Cisharpai.Tests/ --filter "FullyQualifiedName~Testing"
```
