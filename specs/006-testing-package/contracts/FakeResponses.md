# Contract: FakeResponses

**File**: `src/Cisharpai.Testing/FakeResponses.cs`

## Class Definition

```csharp
public static class FakeResponses
```

## Behavior

Static factory methods for creating common response objects with sensible defaults. The constant `DefaultModel` is `"fake-model"`.

### Factory Methods

#### `Chat(content, model?, promptTokens?, completionTokens?)`

Returns a successful `ChatCompletionResponse`.

| Parameter | Default |
|-----------|---------|
| `model` | `"fake-model"` |
| `promptTokens` | `10` |
| `completionTokens` | `5` |

#### `ChatError(errorMessage)`

Returns a failed `ChatCompletionResponse` via `ChatCompletionResponse.Error(errorMessage)`. `IsSuccess=false`, `Content` is empty.

#### `ToolCall(functionName, argumentsJson, id?, model?)`

Returns a `ToolCallingResponse` with one tool call. Parses `argumentsJson` into a `JsonElement`. Auto-generates a GUID for `id` when not provided.

#### `ToolCalls(params (FunctionName, ArgumentsJson)[])`

Returns a `ToolCallingResponse` with multiple tool calls. Each gets an auto-generated GUID `Id`.

#### `GroundedChat(content, citations?, model?)`

Returns a `GroundedChatCompletionResponse` wrapping a successful chat response. Citations default to an empty list.

#### `StreamingChunks(params string[])`

Returns `IReadOnlyList<ChatCompletionChunk>` from text segments. All chunks have `FinishReason=null` except the last, which has `FinishReason="stop"`. Model is `"fake-model"`.

#### `Embedding(vector?, model?, totalTokens?)`

Returns a single-vector `EmbeddingResponse`.

| Parameter | Default |
|-----------|---------|
| `vector` | `[0.1f, 0.2f, 0.3f]` |
| `model` | `"fake-model"` |
| `totalTokens` | `8` |

#### `Embeddings(vectors, model?, totalTokens?)`

Returns a multi-vector `EmbeddingResponse`.

| Parameter | Default |
|-----------|---------|
| `model` | `"fake-model"` |
| `totalTokens` | `16` |

#### `EmbeddingError(errorMessage)`

Returns a failed `EmbeddingResponse` via `EmbeddingResponse.Error(errorMessage)`.
