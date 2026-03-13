# Implementation Plan: Streaming Chat Completions & Vision (Image Input)

Two features, one document. Both follow the established Feature Collection pattern and the library's design principles: no exceptions for API errors, immutable records, backward-compatible additions, provider-agnostic core models.

---

## Part 1: Streaming Chat Completions

### Goal

Expose server-sent event (SSE) streaming for chat completions through a new `IStreamingChatFeature`. Every provider supports SSE-based streaming natively. The implementation yields `ChatCompletionChunk` records via `IAsyncEnumerable<T>`, giving callers token-by-token output without buffering the full response.

### Architecture

#### 1.1 Core Feature Interface

**New file**: `src/Cisharpai/Features/Chat/IStreamingChatFeature.cs`

```csharp
namespace Cisharpai.Features.Chat;

public interface IStreamingChatFeature
{
    IAsyncEnumerable<ChatCompletionChunk> GetChatCompletionStreamAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
```

Pattern matches `IJsonOutputFeature` and `IToolCallingFeature` — a single method, same `ChatCompletionRequest` input, returns a stream of chunks instead of a single response.

#### 1.2 Core Response Model

**New file**: `src/Cisharpai/Models/ChatCompletionChunk.cs`

```csharp
namespace Cisharpai.Models;

/// <summary>
/// A single chunk from a streaming chat completion.
/// </summary>
public sealed record ChatCompletionChunk(
    /// <summary>Content delta text (may be empty for non-content events).</summary>
    string Content,

    /// <summary>Non-null when the stream is finished (e.g. "stop", "tool_calls", "length").</summary>
    string? FinishReason = null,

    /// <summary>Model identifier (typically present on every chunk for OpenAI, first chunk only for Anthropic).</summary>
    string? Model = null,

    /// <summary>Token usage. Only populated on the final chunk when the provider includes it.</summary>
    int? PromptTokens = null,

    /// <summary>Token usage. Only populated on the final chunk when the provider includes it.</summary>
    int? CompletionTokens = null,

    /// <summary>For tool-calling streams: partial tool call deltas.</summary>
    ToolCallDelta? ToolCallDelta = null);

/// <summary>
/// Incremental tool call information emitted during streaming.
/// </summary>
public sealed record ToolCallDelta(
    int Index,
    string? Id,
    string? FunctionName,
    string? ArgumentsDelta);
```

Design notes:
- Immutable records, consistent with all other models.
- `ToolCallDelta` carries partial function name/arguments that accumulate across chunks. Callers concatenate `ArgumentsDelta` strings to reconstruct the full arguments JSON.
- `FinishReason` signals the end of useful content. The stream continues to yield the final chunk, then ends.

#### 1.3 LlmHttpClient SSE Method

**Modified file**: `src/Cisharpai/LlmHttpClient.cs`

Add a new method alongside `PostAsync` and `PostWithRawAsync`:

```csharp
public async IAsyncEnumerable<string> PostStreamAsync<TRequest>(
    string uri,
    TRequest payload,
    [EnumeratorCancellation] CancellationToken cancellationToken = default,
    JsonElement? extraParameters = null)
```

Responsibilities:
1. Serialize the request with `SerializeAndMerge` (reuse existing private method).
2. Send with `HttpCompletionOption.ResponseHeadersRead` (already the pattern).
3. Check status code; throw `LlmHttpRequestException` on non-2xx.
4. Read the response body line-by-line via `StreamReader.ReadLineAsync()`.
5. For each line starting with `data: `, strip the prefix and yield the JSON string.
6. Stop on `data: [DONE]` (OpenAI/Azure convention) or when the stream ends naturally (Anthropic/Cohere).

This method yields raw JSON strings. Each provider is responsible for deserializing into its own delta DTO and mapping to `ChatCompletionChunk`.

**Important**: The `HttpRequestMessage` and `HttpResponseMessage` must **not** be disposed until the enumerable is fully consumed. Use `try/finally` within the `async IAsyncEnumerable` to guarantee disposal when the caller breaks or the `CancellationToken` fires.

#### 1.4 Resilience Handler Considerations

**Modified file**: `src/Cisharpai/HttpClientBuilderExtensions.cs`

The current `AddCisharpaiResilienceHandler()` configures:
- `AttemptTimeout`: 60s
- `TotalRequestTimeout`: 90s

These will cut off any stream longer than 60-90 seconds. Two options:

**Option A (Recommended)**: Add a separate `AddCisharpaiStreamingResilienceHandler()` that disables timeouts but keeps retry and circuit breaker:

```csharp
public static IHttpClientBuilder AddCisharpaiStreamingResilienceHandler(this IHttpClientBuilder builder)
{
    builder.AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout = new HttpTimeoutStrategyOptions { Timeout = Timeout.InfiniteTimeSpan };
        options.TotalRequestTimeout = new HttpTimeoutStrategyOptions { Timeout = Timeout.InfiniteTimeSpan };
        // Keep retry and circuit breaker as-is
    });
    return builder;
}
```

**Option B**: Have `PostStreamAsync` use a raw `HttpClient.SendAsync` that bypasses the resilience pipeline (not ideal, loses retry on connection failure).

Decision: Go with Option A. Streaming-specific DI registrations use the streaming resilience handler. The existing non-streaming handlers remain unchanged.

### Provider SSE Formats

Each provider uses a different SSE envelope. The mapping logic lives entirely within each provider's client.

#### OpenAI (Chat Completions — legacy & reasoning)

Request: add `"stream": true` to `OpenAiChatRequest` / `OpenAiReasoningRequest`.

SSE format:
```
data: {"id":"...","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}],"model":"gpt-4.1-nano"}

data: {"id":"...","choices":[{"index":0,"delta":{},"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":5}}

data: [DONE]
```

**New file**: `src/Cisharpai.OpenAi/Models/OpenAiStreamChunk.cs`
```csharp
public sealed class OpenAiStreamDelta { public string? Content { get; set; } public string? Role { get; set; } /* tool_calls delta fields */ }
public sealed class OpenAiStreamChoice { public OpenAiStreamDelta Delta { get; set; } [JsonPropertyName("finish_reason")] public string? FinishReason { get; set; } public int Index { get; set; } }
public sealed class OpenAiStreamChunk { public string Model { get; set; } public List<OpenAiStreamChoice> Choices { get; set; } public OpenAiUsage? Usage { get; set; } }
```

#### OpenAI (Responses API — GPT-5)

Request: add `"stream": true` to `OpenAiResponsesApiRequest`.

SSE format uses named events:
```
event: response.output_text.delta
data: {"type":"response.output_text.delta","delta":"Hello"}

event: response.completed
data: {"type":"response.completed","response":{...full response...}}
```

**New file**: `src/Cisharpai.OpenAi/Models/OpenAiResponsesStreamEvent.cs`
```csharp
public sealed class OpenAiResponsesStreamEvent { public string Type { get; set; } public string? Delta { get; set; } /* other fields for completed event */ }
```

The Responses API SSE uses `event:` lines. `PostStreamAsync` should yield both the event type and data; or the provider can parse the `type` field from the JSON data payload itself.

#### Azure OpenAI

Identical to OpenAI Chat Completions SSE format. Reuse the same delta DTOs (or clone as `AzureOpenAi` prefixed versions for consistency).

**New file**: `src/Cisharpai.Azure/AzureOpenAi/Models/AzureOpenAiStreamChunk.cs`

#### Azure AI Inference

Same OpenAI-compatible SSE format. Model-dependent support (some catalog models may not support streaming).

**New file**: `src/Cisharpai.Azure/AzureAiInference/Models/AzureAiInferenceStreamChunk.cs`

#### Anthropic

Request: add `"stream": true` to `AnthropicChatRequest`.

SSE format uses named event types:
```
event: content_block_delta
data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}

event: message_delta
data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":15}}

event: message_stop
data: {"type":"message_stop"}
```

**New file**: `src/Cisharpai.Anthropic/Models/AnthropicStreamEvent.cs`
```csharp
public sealed class AnthropicStreamEvent { public string Type { get; set; } public int? Index { get; set; } public AnthropicStreamDelta? Delta { get; set; } public AnthropicUsage? Usage { get; set; } }
public sealed class AnthropicStreamDelta { public string? Type { get; set; } public string? Text { get; set; } [JsonPropertyName("stop_reason")] public string? StopReason { get; set; } }
```

Key difference: Anthropic does **not** send `data: [DONE]`. The stream simply closes after `message_stop`. The `PostStreamAsync` handles this naturally (stream ends → enumeration completes).

#### Cohere

Request: add `"stream": true` to `CohereChatRequest`.

SSE format:
```
event: content-delta
data: {"type":"content-delta","delta":{"message":{"content":{"text":"Hello"}}}}

event: message-end
data: {"type":"message-end","delta":{"finish_reason":"COMPLETE","usage":{...}}}
```

**New file**: `src/Cisharpai.Cohere/Models/CohereStreamEvent.cs`

### Provider Implementation Pattern

Each provider client:

1. Implements `IStreamingChatFeature` alongside its existing interfaces.
2. Registers `features.Set<IStreamingChatFeature>(this)` in the constructor.
3. The `GetChatCompletionStreamAsync` method:
   a. Builds the provider-specific request (reuse existing `BuildRequest`-style logic).
   b. Adds `stream = true` to the request DTO.
   c. Calls `_client.PostStreamAsync(endpoint, request, ct)`.
   d. Deserializes each yielded JSON line into the provider's stream chunk DTO.
   e. Maps to `ChatCompletionChunk` and yields it.
   f. On error, yields a final chunk or throws (TBD — see error handling).

### Error Handling

The library's principle is "no exceptions for API errors". For streaming:

- **HTTP errors** (4xx/5xx): `PostStreamAsync` throws `LlmHttpRequestException` before any chunks are yielded (status is checked on headers). Each provider catches this and yields a single error chunk:
  ```csharp
  yield return new ChatCompletionChunk(Content: string.Empty, FinishReason: "error", ...);
  ```
  Alternatively, wrap the stream in a try/catch at the provider level and return an `IAsyncEnumerable` that starts with an error signal. Callers can check `FinishReason == "error"`.

- **Mid-stream errors** (connection drops, malformed SSE): Yield what was received so far, then let the `IAsyncEnumerable` complete. The caller sees the stream end without a `FinishReason` of "stop" — they can detect incomplete responses.

### DI Registration

No new DI methods needed. The existing `AddOpenAiClient()`, etc., already register the concrete client class which will now also implement `IStreamingChatFeature`. Feature discovery via `client.Features.Get<IStreamingChatFeature>()` works automatically.

However, if streaming-specific resilience is desired, a DI overload or option flag (e.g., `options.EnableStreaming = true`) could switch to the streaming resilience handler. Alternatively, callers configure their own `HttpClient` timeout to `Timeout.InfiniteTimeSpan` when they know they'll use streaming.

### Tasks

#### Phase 1: Core Infrastructure
1. [ ] Create `src/Cisharpai/Features/Chat/IStreamingChatFeature.cs`
2. [ ] Create `src/Cisharpai/Models/ChatCompletionChunk.cs` with `ChatCompletionChunk` and `ToolCallDelta`
3. [ ] Add `PostStreamAsync<TRequest>` method to `src/Cisharpai/LlmHttpClient.cs`
4. [ ] Add `AddCisharpaiStreamingResilienceHandler()` to `src/Cisharpai/HttpClientBuilderExtensions.cs`
5. [ ] Unit tests for `PostStreamAsync` SSE line parsing (mock `HttpMessageHandler`)

#### Phase 2: OpenAI
6. [ ] Add `Stream` property to `OpenAiChatRequest`, `OpenAiReasoningRequest`, `OpenAiResponsesApiRequest`
7. [ ] Create `src/Cisharpai.OpenAi/Models/OpenAiStreamChunk.cs` (delta DTOs)
8. [ ] Create `src/Cisharpai.OpenAi/Models/OpenAiResponsesStreamEvent.cs` (Responses API delta DTOs)
9. [ ] Implement `IStreamingChatFeature` in `OpenAiChatCompletionClient`
10. [ ] Unit tests: request serialization with `stream: true`, chunk deserialization, mapping to `ChatCompletionChunk`
11. [ ] Update feature discovery tests

#### Phase 3: Azure OpenAI
12. [ ] Add `Stream` property to `AzureOpenAiChatRequest`, `AzureOpenAiReasoningChatRequest`
13. [ ] Create `src/Cisharpai.Azure/AzureOpenAi/Models/AzureOpenAiStreamChunk.cs`
14. [ ] Implement `IStreamingChatFeature` in `AzureOpenAiChatCompletionClient`
15. [ ] Unit tests and feature discovery tests

#### Phase 4: Azure AI Inference
16. [ ] Add `Stream` property to `AzureAiInferenceChatRequest`, `AzureAiInferenceReasoningChatRequest`
17. [ ] Create `src/Cisharpai.Azure/AzureAiInference/Models/AzureAiInferenceStreamChunk.cs`
18. [ ] Implement `IStreamingChatFeature` in `AzureAiInferenceChatCompletionClient`
19. [ ] Unit tests and feature discovery tests

#### Phase 5: Anthropic
20. [ ] Add `Stream` property to `AnthropicChatRequest`
21. [ ] Create `src/Cisharpai.Anthropic/Models/AnthropicStreamEvent.cs`
22. [ ] Implement `IStreamingChatFeature` in `AnthropicChatCompletionClient`
23. [ ] Unit tests and feature discovery tests

#### Phase 6: Cohere
24. [ ] Add `Stream` property to `CohereChatRequest`
25. [ ] Create `src/Cisharpai.Cohere/Models/CohereStreamEvent.cs`
26. [ ] Implement `IStreamingChatFeature` in `CohereChatCompletionClient`
27. [ ] Unit tests and feature discovery tests

#### Phase 7: Integration & Documentation
28. [ ] Integration tests for all providers (basic stream, verify content accumulates, verify finish reason)
29. [ ] Add streaming console scenario to `src/Cisharp.Console/Scenarios/`
30. [ ] Create `wiki/streaming.md` documentation
31. [ ] Update `wiki/provider-features.md` feature matrix
32. [ ] Update `memories/project_overview.md`

---

## Part 2: Vision / Image Input in Chat Messages

### Goal

Allow `LlmMessage` to carry mixed text + image content for multimodal chat completions. Images are accepted as local file paths or raw base64 data. This is an additive, backward-compatible change to the core model. Providers that don't support vision (Cohere) skip or reject image parts gracefully.

### Architecture

#### 2.1 Core Content Part Model

**New file**: `src/Cisharpai/Models/MessageContentPart.cs`

```csharp
namespace Cisharpai.Models;

/// <summary>
/// A part of a multimodal message. Used in <see cref="LlmMessage.ContentParts"/>.
/// </summary>
public abstract record MessageContentPart;

/// <summary>Text content in a multimodal message.</summary>
public sealed record TextContentPart(string Text) : MessageContentPart;

/// <summary>Image from a local file path. Automatically converted to base64 by the provider client.</summary>
public sealed record ImageFileContentPart(string FilePath) : MessageContentPart;

/// <summary>Image from raw base64 data with explicit MIME type.</summary>
public sealed record ImageBase64ContentPart(
    string Base64Data,
    string MediaType) : MessageContentPart;
```

This mirrors the `EmbeddingContentPart` hierarchy in `MultimodalEmbeddingInput.cs` but is specific to chat messages. Two concrete types for the two accepted input modes (file path and base64), as agreed.

#### 2.2 LlmMessage Change

**Modified file**: `src/Cisharpai/Models/LlmMessage.cs`

```csharp
public sealed record LlmMessage(
    LlmRole Role,
    string Content,
    string? ToolCallId = null,
    IReadOnlyList<ToolCall>? ToolCalls = null,

    /// <summary>
    /// Multimodal content parts. When non-null, providers use this instead of <see cref="Content"/>
    /// for building the request. Text-only callers continue using <see cref="Content"/> as before.
    /// </summary>
    IReadOnlyList<MessageContentPart>? ContentParts = null);
```

**Backward compatibility**: All existing code using `new LlmMessage(LlmRole.User, "hello")` continues to work unchanged. `ContentParts` defaults to `null`, and providers check it before falling back to `Content`.

**Convenience factory method** (optional, on `LlmMessage`):
```csharp
public static LlmMessage WithImage(string text, string imagePath) =>
    new(LlmRole.User, string.Empty, ContentParts: new MessageContentPart[]
    {
        new TextContentPart(text),
        new ImageFileContentPart(imagePath)
    });

public static LlmMessage WithBase64Image(string text, string base64Data, string mediaType) =>
    new(LlmRole.User, string.Empty, ContentParts: new MessageContentPart[]
    {
        new TextContentPart(text),
        new ImageBase64ContentPart(base64Data, mediaType)
    });
```

#### 2.3 ImageDataUriHelper — Move to Core

**Current location**: `src/Cisharpai.Cohere/ImageDataUriHelper.cs` (internal)

**New location**: `src/Cisharpai/ImageDataUriHelper.cs` (public)

The helper converts file paths to data URIs (`data:{mime};base64,{base64}`) and detects MIME types. It's needed by OpenAI, Azure, and Anthropic for vision, and Cohere for embeddings. Making it public in core avoids duplication.

```csharp
namespace Cisharpai;

public static class ImageDataUriHelper
{
    public static async Task<string> ToDataUriAsync(string imagePath, CancellationToken ct = default);
    public static string GetMimeType(string path);
    public static string GetMimeTypeFromMediaType(string mediaType); // passthrough validation
}
```

The Cohere project changes its `using` to reference the core namespace. Existing Cohere tests remain valid.

#### 2.4 Provider Message DTO Changes

The key change in each provider is: the `Content` field on the message DTO must accept **either** a string **or** a JSON array of content parts. `System.Text.Json` serializes `object` polymorphically, so changing the DTO property type to `object?` handles both cases.

##### OpenAI

**Modified file**: `src/Cisharpai.OpenAi/Models/OpenAiChatMessage.cs`

```csharp
// Change from:
public string? Content { get; set; }
// To:
public object? Content { get; set; }
```

**New file**: `src/Cisharpai.OpenAi/Models/OpenAiContentPart.cs`
```csharp
namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiContentPart
{
    public string Type { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiImageUrl? ImageUrl { get; set; }
}

public sealed class OpenAiImageUrl
{
    public string Url { get; set; } = string.Empty;
}
```

OpenAI vision format:
```json
{
  "role": "user",
  "content": [
    { "type": "text", "text": "What is in this image?" },
    { "type": "image_url", "image_url": { "url": "data:image/png;base64,..." } }
  ]
}
```

##### Azure OpenAI

**Modified file**: `src/Cisharpai.Azure/AzureOpenAi/Models/AzureOpenAiChatRequest.cs`

Same change: `AzureOpenAiChatMessage.Content` from `string?` to `object?`.

**New file**: `src/Cisharpai.Azure/AzureOpenAi/Models/AzureOpenAiContentPart.cs`

Identical structure to OpenAI (same API format).

##### Azure AI Inference

**Modified file**: `src/Cisharpai.Azure/AzureAiInference/Models/AzureAiInferenceChatRequest.cs`

Same change: `AzureAiInferenceChatMessage.Content` from `string?` to `object?`.

**New file**: `src/Cisharpai.Azure/AzureAiInference/Models/AzureAiInferenceContentPart.cs`

Same OpenAI-compatible format.

##### Anthropic

**No DTO type change needed**: `AnthropicMessage.Content` is already `object`.

**Modified file**: `src/Cisharpai.Anthropic/Models/AnthropicChatResponse.cs` (add image source class)

**New file**: `src/Cisharpai.Anthropic/Models/AnthropicImageSource.cs`
```csharp
namespace Cisharpai.Anthropic.Models;

public sealed class AnthropicImageSource
{
    public string Type { get; set; } = "base64";

    [JsonPropertyName("media_type")]
    public string MediaType { get; set; } = string.Empty;

    public string Data { get; set; } = string.Empty;
}
```

Add `Source` property to existing `AnthropicContentBlock`:
```csharp
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public AnthropicImageSource? Source { get; set; }
```

Anthropic vision format:
```json
{
  "role": "user",
  "content": [
    { "type": "text", "text": "What is in this image?" },
    { "type": "image", "source": { "type": "base64", "media_type": "image/png", "data": "..." } }
  ]
}
```

##### Cohere

**No changes to chat DTOs**. Cohere's v2 Chat API does **not** support image inputs in messages. The mapper will skip image parts and log/warn, or throw `NotSupportedException` if all content parts are images and no text remains.

#### 2.5 Provider MapMessages Changes

Each provider's private `MapMessages()` method (or equivalent) currently does `msg.Content = m.Content`. It needs a new branch:

```csharp
// Pseudo-code (OpenAI pattern — similar for Azure variants)
private async Task<List<OpenAiChatMessage>> MapMessagesAsync(
    IReadOnlyList<LlmMessage> messages, CancellationToken ct)
{
    var result = new List<OpenAiChatMessage>();
    foreach (var m in messages)
    {
        var msg = new OpenAiChatMessage { Role = MapRole(m.Role) };

        if (m.ContentParts is { Count: > 0 })
        {
            var parts = new List<OpenAiContentPart>();
            foreach (var part in m.ContentParts)
            {
                switch (part)
                {
                    case TextContentPart text:
                        parts.Add(new OpenAiContentPart { Type = "text", Text = text.Text });
                        break;
                    case ImageFileContentPart file:
                        var dataUri = await ImageDataUriHelper.ToDataUriAsync(file.FilePath, ct);
                        parts.Add(new OpenAiContentPart
                        {
                            Type = "image_url",
                            ImageUrl = new OpenAiImageUrl { Url = dataUri }
                        });
                        break;
                    case ImageBase64ContentPart base64:
                        parts.Add(new OpenAiContentPart
                        {
                            Type = "image_url",
                            ImageUrl = new OpenAiImageUrl
                            {
                                Url = $"data:{base64.MediaType};base64,{base64.Base64Data}"
                            }
                        });
                        break;
                }
            }
            msg.Content = parts;
        }
        else
        {
            msg.Content = m.Content;
        }

        // ... existing tool call / tool result mapping ...
        result.Add(msg);
    }
    return result;
}
```

**Async change**: `MapMessages` becomes `MapMessagesAsync` because `ImageDataUriHelper.ToDataUriAsync` reads files. This is a mechanical refactor — the calling `GetChatCompletionAsync` methods are already async. All call sites (regular chat, JSON output, tool calling) must be updated to `await MapMessagesAsync(...)`.

For **Anthropic**, the mapping is different (content blocks instead of content parts):

```csharp
case ImageFileContentPart file:
    var bytes = await File.ReadAllBytesAsync(file.FilePath, ct);
    blocks.Add(new AnthropicContentBlock
    {
        Type = "image",
        Source = new AnthropicImageSource
        {
            MediaType = ImageDataUriHelper.GetMimeType(file.FilePath),
            Data = Convert.ToBase64String(bytes)
        }
    });
    break;
case ImageBase64ContentPart base64:
    blocks.Add(new AnthropicContentBlock
    {
        Type = "image",
        Source = new AnthropicImageSource
        {
            MediaType = base64.MediaType,
            Data = base64.Base64Data
        }
    });
    break;
```

For **Cohere**, image parts are silently skipped (only text parts are mapped). If the message contains only image parts and no text, the message is omitted with a clear exception.

### Provider Support Matrix (Vision)

| Provider | Vision Support | Image Format |
|---|---|---|
| OpenAI | Yes (GPT-4o, GPT-4.1, GPT-5) | `image_url` content part with data URI |
| Azure OpenAI | Yes (same as OpenAI) | `image_url` content part with data URI |
| Azure AI Inference | Model-dependent | `image_url` content part with data URI |
| Anthropic | Yes (Claude 3+) | `image` content block with base64 source |
| Cohere | No (chat v2 has no vision) | N/A — image parts skipped |

### Tasks

#### Phase 1: Core Models
1. [ ] Create `src/Cisharpai/Models/MessageContentPart.cs` (`MessageContentPart`, `TextContentPart`, `ImageFileContentPart`, `ImageBase64ContentPart`)
2. [ ] Add `ContentParts` parameter to `LlmMessage` in `src/Cisharpai/Models/LlmMessage.cs`
3. [ ] Add static factory methods (`WithImage`, `WithBase64Image`) to `LlmMessage`
4. [ ] Move `ImageDataUriHelper` from `src/Cisharpai.Cohere/ImageDataUriHelper.cs` to `src/Cisharpai/ImageDataUriHelper.cs`, make it `public`
5. [ ] Update Cohere project to use the core `ImageDataUriHelper` (remove local copy, update `using`)
6. [ ] Unit tests for `MessageContentPart` types, `LlmMessage` backward compatibility

#### Phase 2: OpenAI Vision
7. [ ] Create `src/Cisharpai.OpenAi/Models/OpenAiContentPart.cs` (`OpenAiContentPart`, `OpenAiImageUrl`)
8. [ ] Change `OpenAiChatMessage.Content` from `string?` to `object?`
9. [ ] Refactor `MapMessages` to `MapMessagesAsync` in `OpenAiChatCompletionClient`, handle `ContentParts`
10. [ ] Update all call sites (`GetChatCompletionAsync`, `GetChatCompletionWithJsonOutputAsync`, `GetChatCompletionWithToolsAsync`) to `await MapMessagesAsync`
11. [ ] Unit tests: request serialization with image parts (file path and base64), text-only backward compat

#### Phase 3: Azure OpenAI Vision
12. [ ] Create `src/Cisharpai.Azure/AzureOpenAi/Models/AzureOpenAiContentPart.cs`
13. [ ] Change `AzureOpenAiChatMessage.Content` from `string?` to `object?`
14. [ ] Refactor `MapMessages` to `MapMessagesAsync` in `AzureOpenAiChatCompletionClient`
15. [ ] Update all call sites
16. [ ] Unit tests

#### Phase 4: Azure AI Inference Vision
17. [ ] Create `src/Cisharpai.Azure/AzureAiInference/Models/AzureAiInferenceContentPart.cs`
18. [ ] Change `AzureAiInferenceChatMessage.Content` from `string?` to `object?`
19. [ ] Refactor `MapMessages` to `MapMessagesAsync` in `AzureAiInferenceChatCompletionClient`
20. [ ] Update all call sites
21. [ ] Unit tests

#### Phase 5: Anthropic Vision
22. [ ] Create `src/Cisharpai.Anthropic/Models/AnthropicImageSource.cs`
23. [ ] Add `Source` property to `AnthropicContentBlock` in `AnthropicChatResponse.cs`
24. [ ] Refactor `MapMessages` to `MapMessagesAsync` in `AnthropicChatCompletionClient`, handle image content blocks
25. [ ] Update all call sites
26. [ ] Unit tests

#### Phase 6: Cohere (Skip/Warn)
27. [ ] Update `CohereChatCompletionClient.MapMessages` to skip image content parts, throw if message becomes empty
28. [ ] Unit tests for skip behavior

#### Phase 7: Integration & Documentation
29. [ ] Integration tests (OpenAI + Anthropic: send simple image, verify model describes it)
30. [ ] Add vision console scenario to `src/Cisharp.Console/Scenarios/`
31. [ ] Create `wiki/vision.md` documentation
32. [ ] Update `wiki/provider-features.md` feature matrix
33. [ ] Update `memories/project_overview.md`

---

## Cross-Cutting Concerns

### Test Image Assets

Both unit and integration tests need test images. For unit tests, use a small programmatically-generated PNG (64x64 solid color) created in a test fixture — no checked-in binary files. For integration tests, same approach or a small image file in the test project.

### ExtraParameters Compatibility

Both features work with the existing `ExtraParameters` deep-merge escape hatch:
- Streaming: `ExtraParameters` can override `stream` or add `stream_options` (e.g., OpenAI's `include_usage`).
- Vision: `ExtraParameters` can add provider-specific image options (e.g., OpenAI's `detail: "high"` on image_url).

### Package Dependencies

No new NuGet dependencies. `IAsyncEnumerable<T>` is available in .NET 8+ natively. `System.Runtime.CompilerServices.EnumeratorCancellationAttribute` is in the BCL.

### Feature Discovery Tests

**Modified file**: `src/Cisharpai.Tests/Features/FeatureDiscoveryTests.cs`

Add assertions for `IStreamingChatFeature` on all five provider clients, mirroring the existing `IJsonOutputFeature` and `IToolCallingFeature` discovery tests.

### Wiki Updates

| File | Change |
|---|---|
| `wiki/provider-features.md` | Add "Streaming" and "Vision" rows to the feature matrix |
| `wiki/streaming.md` | New page: quick start, chunk model, error handling, provider notes |
| `wiki/vision.md` | New page: quick start, content parts, file path vs base64, provider support |
| `wiki/index.md` | Add links to new pages |
| `README.md` | Add streaming and vision to feature list, add wiki links |

---

## Implementation Order

Recommended order: **Vision first, then Streaming**.

Vision is a simpler change (additive parameter, no new infrastructure) and validates the `MapMessagesAsync` refactor that streaming will also need. Streaming requires new HTTP plumbing (`PostStreamAsync`) and is more complex to test.

Alternatively, both can be developed in parallel on separate branches since they touch different parts of the codebase (vision modifies message mapping; streaming adds a new HTTP method and feature interface).
