# Data Model: Testing Package

## Fake Client Classes

### FakeChatCompletionClient

Sealed class. Implements `IChatCompletionClient`, `IStreamingChatFeature`, `IToolCallingFeature`, `IJsonOutputFeature`, `IGroundedChatFeature`.

**Internal State (mutable — test-time only)**:

| Field | Type | Purpose |
|-------|------|---------|
| `_responses` | `Queue<ChatCompletionResponse>` | FIFO queue for `GetChatCompletionAsync` |
| `_toolCallingResponses` | `Queue<ToolCallingResponse>` | FIFO queue for `GetChatCompletionWithToolsAsync` |
| `_jsonOutputResponses` | `Queue<ChatCompletionResponse>` | FIFO queue for `GetChatCompletionWithJsonOutputAsync` |
| `_groundedChatResponses` | `Queue<GroundedChatCompletionResponse>` | FIFO queue for `GetGroundedChatCompletionAsync` |
| `_streamingResponses` | `Queue<IReadOnlyList<ChatCompletionChunk>>` | FIFO queue for `GetChatCompletionStreamAsync` |
| `_receivedRequests` | `List<ChatCompletionRequest>` | Captures chat requests |
| `_receivedToolCallingRequests` | `List<(ChatCompletionRequest, ToolCallingOptions)>` | Captures tool calling requests + options |
| `_receivedJsonOutputRequests` | `List<(ChatCompletionRequest, JsonOutputOptions)>` | Captures JSON output requests + options |
| `_receivedGroundedChatRequests` | `List<(ChatCompletionRequest, GroundedChatOptions)>` | Captures grounded chat requests + options |
| `_receivedStreamingRequests` | `List<ChatCompletionRequest>` | Captures streaming requests |

**Public Configuration Properties**:

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `DefaultResponse` | `ChatCompletionResponse?` | `null` | Fallback for chat and JSON output (when `DefaultJsonOutputResponse` is null) |
| `DefaultJsonOutputResponse` | `ChatCompletionResponse?` | `null` | Overrides `DefaultResponse` for JSON output |
| `DefaultToolCallingResponse` | `ToolCallingResponse?` | `null` | Fallback for tool calling |
| `DefaultGroundedChatResponse` | `GroundedChatCompletionResponse?` | `null` | Fallback for grounded chat |
| `DefaultStreamingResponse` | `IReadOnlyList<ChatCompletionChunk>?` | `null` | Fallback for streaming |

**Read-Only Capture Properties**:

| Property | Type |
|----------|------|
| `ReceivedRequests` | `IReadOnlyList<ChatCompletionRequest>` |
| `ReceivedToolCallingRequests` | `IReadOnlyList<(ChatCompletionRequest Request, ToolCallingOptions Options)>` |
| `ReceivedJsonOutputRequests` | `IReadOnlyList<(ChatCompletionRequest Request, JsonOutputOptions Options)>` |
| `ReceivedGroundedChatRequests` | `IReadOnlyList<(ChatCompletionRequest Request, GroundedChatOptions Options)>` |
| `ReceivedStreamingRequests` | `IReadOnlyList<ChatCompletionRequest>` |
| `CallCount` | `int` (sum of all capture list counts) |

**Constructor**: `FakeChatCompletionClient(FakeChatFeatures enabledFeatures = FakeChatFeatures.All)` — registers `this` into a `FeatureCollection` for each flag set.

### FakeEmbeddingClient

Sealed class. Implements `IEmbeddingClient`, `IImageEmbeddingFeature`, `IMultimodalEmbeddingFeature`.

**Internal State (mutable — test-time only)**:

| Field | Type | Purpose |
|-------|------|---------|
| `_responses` | `Queue<EmbeddingResponse>` | FIFO queue for `GetEmbeddingsAsync` |
| `_imageResponses` | `Queue<EmbeddingResponse>` | FIFO queue for `GetImageEmbeddingAsync` |
| `_multimodalResponses` | `Queue<EmbeddingResponse>` | FIFO queue for `GetMultimodalEmbeddingsAsync` |
| `_receivedRequests` | `List<EmbeddingRequest>` | Captures embedding requests |
| `_receivedImageRequests` | `List<(string ImagePath, string Model)>` | Captures image embedding requests |
| `_receivedMultimodalRequests` | `List<IReadOnlyList<MultimodalEmbeddingInput>>` | Captures multimodal requests |

**Public Configuration Properties**:

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `DefaultResponse` | `EmbeddingResponse?` | `null` | Fallback for all methods when specific default is null |
| `DefaultImageResponse` | `EmbeddingResponse?` | `null` | Overrides `DefaultResponse` for image embedding |
| `DefaultMultimodalResponse` | `EmbeddingResponse?` | `null` | Overrides `DefaultResponse` for multimodal embedding |

**Constructor**: `FakeEmbeddingClient(FakeEmbeddingFeatures enabledFeatures = FakeEmbeddingFeatures.All)`

## Enums

### FakeChatFeatures

`[Flags]` enum controlling feature registration on `FakeChatCompletionClient`.

| Value | Bit | Maps to |
|-------|-----|---------|
| `None` | `0` | No features registered |
| `Streaming` | `1 << 0` | `IStreamingChatFeature` |
| `ToolCalling` | `1 << 1` | `IToolCallingFeature` |
| `JsonOutput` | `1 << 2` | `IJsonOutputFeature` |
| `GroundedChat` | `1 << 3` | `IGroundedChatFeature` |
| `All` | `Streaming \| ToolCalling \| JsonOutput \| GroundedChat` | All features |

### FakeEmbeddingFeatures

`[Flags]` enum controlling feature registration on `FakeEmbeddingClient`.

| Value | Bit | Maps to |
|-------|-----|---------|
| `None` | `0` | No features registered |
| `ImageEmbedding` | `1 << 0` | `IImageEmbeddingFeature` |
| `MultimodalEmbedding` | `1 << 1` | `IMultimodalEmbeddingFeature` |
| `All` | `ImageEmbedding \| MultimodalEmbedding` | All features |

## Static Factory: FakeResponses

Static class with factory methods returning pre-built response objects.

| Method | Return Type | Defaults |
|--------|-------------|----------|
| `Chat(content, model?, promptTokens?, completionTokens?)` | `ChatCompletionResponse` | model=`"fake-model"`, promptTokens=10, completionTokens=5 |
| `ChatError(errorMessage)` | `ChatCompletionResponse` | `IsSuccess=false` |
| `ToolCall(functionName, argumentsJson, id?, model?)` | `ToolCallingResponse` | Auto-generated GUID for id |
| `ToolCalls(params (FunctionName, ArgumentsJson)[])` | `ToolCallingResponse` | Multiple tool calls |
| `GroundedChat(content, citations?, model?)` | `GroundedChatCompletionResponse` | Empty citations list |
| `StreamingChunks(params string[])` | `IReadOnlyList<ChatCompletionChunk>` | Last chunk has `FinishReason="stop"` |
| `Embedding(vector?, model?, totalTokens?)` | `EmbeddingResponse` | vector=`[0.1f, 0.2f, 0.3f]`, totalTokens=8 |
| `Embeddings(vectors, model?, totalTokens?)` | `EmbeddingResponse` | totalTokens=16 |
| `EmbeddingError(errorMessage)` | `EmbeddingResponse` | `IsSuccess=false` |

## DI Extensions: FakeServiceCollectionExtensions

Static class with extension methods on `IServiceCollection`.

| Method | Returns | Behavior |
|--------|---------|----------|
| `AddFakeChatCompletionClient(enabledFeatures?)` | `FakeChatCompletionClient` | Registers as singleton `IChatCompletionClient`; returns instance for setup |
| `AddFakeEmbeddingClient(enabledFeatures?)` | `FakeEmbeddingClient` | Registers as singleton `IEmbeddingClient`; returns instance for setup |

## Entity Relationships

```
FakeChatCompletionClient ──implements──▶ IChatCompletionClient
    ├──implements──▶ IStreamingChatFeature    (if FakeChatFeatures.Streaming)
    ├──implements──▶ IToolCallingFeature       (if FakeChatFeatures.ToolCalling)
    ├──implements──▶ IJsonOutputFeature        (if FakeChatFeatures.JsonOutput)
    └──implements──▶ IGroundedChatFeature      (if FakeChatFeatures.GroundedChat)

FakeEmbeddingClient ──implements──▶ IEmbeddingClient
    ├──implements──▶ IImageEmbeddingFeature    (if FakeEmbeddingFeatures.ImageEmbedding)
    └──implements──▶ IMultimodalEmbeddingFeature (if FakeEmbeddingFeatures.MultimodalEmbedding)

FakeResponses ──creates──▶ ChatCompletionResponse
FakeResponses ──creates──▶ ToolCallingResponse
FakeResponses ──creates──▶ GroundedChatCompletionResponse
FakeResponses ──creates──▶ ChatCompletionChunk[]
FakeResponses ──creates──▶ EmbeddingResponse

FakeServiceCollectionExtensions ──registers──▶ FakeChatCompletionClient as IChatCompletionClient
FakeServiceCollectionExtensions ──registers──▶ FakeEmbeddingClient as IEmbeddingClient
```
