# Testing with Fake Clients

## Installation

```xml
<PackageReference Include="Cisharpai.Testing" />
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
fake.ResponseQueue.Enqueue(FakeResponses.Chat("First"));
fake.ResponseQueue.Enqueue(FakeResponses.Chat("Second"));
fake.ResponseQueue.Enqueue(FakeResponses.ChatError("Oops"));
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
fake.ResponseQueue.Enqueue(FakeResponses.Chat("First call"));
fake.ResponseQueue.Enqueue(FakeResponses.Chat("Second call"));
fake.DefaultResponse = FakeResponses.Chat("All subsequent calls");
```
