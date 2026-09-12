# Testing with Fake Clients

## Installation

```xml
<PackageReference Include="Cisharpai.Testing" />
```

If the consuming project uses Central Package Management, also add a matching
`<PackageVersion Include="Cisharpai.Testing" Version="..." />` in
`Directory.Packages.props`.

Most tests also need:

```csharp
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.Testing;
```

## FakeResponses Factory

```csharp
// Chat
FakeResponses.Chat("Hello!")
FakeResponses.Chat("Hello!", "gpt-4o", promptTokens: 10, completionTokens: 5)
FakeResponses.ChatError("Rate limit exceeded")

// Tool Calling
FakeResponses.ToolCall("get_weather", """{"location":"Paris"}""")
FakeResponses.ToolCall("get_weather", """{"location":"Paris"}""", "call_123")
FakeResponses.ToolCalls(("get_weather", """{"location":"Paris"}"""), ("get_time", "{}"))

// Grounded Chat
FakeResponses.GroundedChat("Answer with citations")
FakeResponses.GroundedChat("Answer", citations)

// Streaming
FakeResponses.StreamingChunks("Hello", " ", "world", "!")

// Embeddings
FakeResponses.Embedding()
FakeResponses.Embedding(new float[] { 0.1f, 0.2f, 0.3f })
FakeResponses.Embeddings(vector1, vector2)
FakeResponses.EmbeddingError("Invalid input")

// Reranking
FakeResponses.Rerank((1, 0.99), (0, 0.42))   // explicit (index, score) pairs, ranked order
FakeResponses.Rerank(documentCount: 3)        // N docs in original order, descending scores
FakeResponses.RerankError("Invalid input")
```

## FakeChatCompletionClient

### Default Response

```csharp
var fake = new FakeChatCompletionClient
{
    DefaultResponse = FakeResponses.Chat("Default answer")
};
```

### Response Queue (FIFO)

```csharp
fake.EnqueueResponse(FakeResponses.Chat("First"));
fake.EnqueueResponse(FakeResponses.Chat("Second"));
fake.EnqueueResponse(FakeResponses.ChatError("Oops"));
// Queue is consumed first, then falls back to DefaultResponse
```

### Feature-Specific Defaults

```csharp
fake.DefaultJsonOutputResponse = FakeResponses.Chat("""{"color":"blue"}""");
fake.DefaultToolCallingResponse = FakeResponses.ToolCall("get_weather", "{}");
fake.DefaultGroundedChatResponse = FakeResponses.GroundedChat("Grounded answer");
fake.DefaultStreamingResponse = FakeResponses.StreamingChunks("Hello", " world");
```

### Request Capture

```csharp
await fake.GetChatCompletionAsync(request);

Assert.Single(fake.ReceivedRequests);
Assert.Equal("Hello", fake.ReceivedRequests[0].Messages[0].Content);

// Feature-specific capture
fake.ReceivedToolCallingRequests   // tool calling requests
fake.ReceivedJsonOutputRequests    // JSON output requests
fake.ReceivedGroundedChatRequests  // grounded chat requests
fake.ReceivedStreamingRequests     // streaming requests
```

### CallCount & Reset

```csharp
Assert.Equal(3, fake.CallCount);  // total across all methods
fake.Reset();  // clears queues and captured requests
```

## FakeEmbeddingClient

```csharp
var fake = new FakeEmbeddingClient
{
    DefaultResponse = FakeResponses.Embedding(new float[] { 0.1f, 0.2f })
};

// Feature-specific
fake.DefaultImageResponse = FakeResponses.Embedding();
fake.DefaultMultimodalResponse = FakeResponses.Embedding();

// Request capture
fake.ReceivedRequests
fake.ReceivedImageRequests
fake.ReceivedMultimodalRequests
```

## Selective Feature Registration

```csharp
// Only streaming + tool calling
var fake = new FakeChatCompletionClient(
    FakeChatFeatures.Streaming | FakeChatFeatures.ToolCalling);

// All features
var fake = new FakeChatCompletionClient(FakeChatFeatures.All);

// No features
var fake = new FakeChatCompletionClient(FakeChatFeatures.None);
```

**FakeChatFeatures:** `Streaming`, `ToolCalling`, `JsonOutput`, `GroundedChat`, `All`, `None`

**FakeEmbeddingFeatures:** `ImageEmbedding`, `MultimodalEmbedding`, `All`, `None`

## DI Registration

```csharp
// Register fake
var fake = services.AddFakeChatCompletionClient();
fake.DefaultResponse = FakeResponses.Chat("Test");

// With selective features
var fake = services.AddFakeChatCompletionClient(FakeChatFeatures.Streaming);

// Embedding
var fake = services.AddFakeEmbeddingClient();
```

## Testing Patterns

### Service Under Test

```csharp
var fake = services.AddFakeChatCompletionClient();
fake.DefaultResponse = FakeResponses.Chat("42");

var sut = provider.GetRequiredService<MyService>();
var result = await sut.AskQuestion("What is 6x7?");

Assert.Equal("42", result);
Assert.Single(fake.ReceivedRequests);
```

### Error Handling

```csharp
fake.DefaultResponse = FakeResponses.ChatError("Rate limited");
var result = await sut.AskQuestion("Hello");
Assert.Contains("error", result, StringComparison.OrdinalIgnoreCase);
```

### Feature Detection

```csharp
var fake = new FakeChatCompletionClient(FakeChatFeatures.None);
var streaming = fake.Features.Get<IStreamingChatFeature>();
Assert.Null(streaming);  // feature not registered
```

### Sequential Responses

```csharp
fake.EnqueueResponse(FakeResponses.Chat("First call"));
fake.EnqueueResponse(FakeResponses.Chat("Second call"));
fake.DefaultResponse = FakeResponses.Chat("All subsequent calls");
```

## FakeRerankerClient

Same queue/default/capture shape as the other fakes. `IRerankerClient` has no optional feature
interfaces, so there are no feature flags.

```csharp
var fake = new FakeRerankerClient
{
    DefaultResponse = FakeResponses.Rerank((1, 0.99), (0, 0.42))
};

string[] documents = ["Nevada's capital is Carson City.", "Paris is the capital of France."];
var response = await fake.RerankAsync(new RerankRequest("What is the capital of France?", documents));

Assert.Equal(documents[1], documents[response.Results[0].Index]);
Assert.Single(fake.ReceivedRequests);
Assert.Equal("What is the capital of France?", fake.ReceivedRequests[0].Query);
```

Members: `DefaultResponse`, `EnqueueResponse(response)`, `ReceivedRequests`, `CallCount`,
`Reset()`. DI helper: `services.AddFakeRerankerClient()`.

Calling `RerankAsync` with neither a queued response nor a default throws
`InvalidOperationException`.

## Request DTO Syntax

`ChatCompletionRequest` and `LlmMessage` are immutable positional records:

```csharp
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "What is 6x7?")],
    Model: "gpt-4o");
```

Do not use object initializers for `ChatCompletionRequest`, and do not pass
string roles such as `"user"` to `LlmMessage`.
