# Contract: IStreamingChatFeature

**File**: `src/Cisharpai/Features/Chat/IStreamingChatFeature.cs`

## Interface

```csharp
public interface IStreamingChatFeature
{
    IAsyncEnumerable<ChatCompletionChunk> GetChatCompletionStreamAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
```

## Discovery

Streaming is an optional feature discovered via the Feature Collection pattern:

```csharp
IChatCompletionClient client = /* any provider */;
var feature = client.Features.Get<IStreamingChatFeature>();
if (feature is not null)
{
    await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
    {
        // process chunk
    }
}
```

All 5 provider clients implement `IStreamingChatFeature` directly (the client IS the feature).
The feature is registered in each client's constructor via `features.Set<IStreamingChatFeature>(this)`.

## Input: ChatCompletionRequest

Standard `ChatCompletionRequest` record — same as non-streaming. The streaming
implementation adds `stream: true` (and `stream_options` where applicable) to the
wire request internally. The caller does not set stream parameters.

Key fields used by streaming:
- `Messages` — required; the conversation to complete
- `Model` — required; determines API routing (legacy vs Responses API, standard vs reasoning)
- `MaxTokens` — optional; mapped to `max_completion_tokens` for reasoning models
- `ExtraParameters` — optional; deep-merged into the wire request
- `Temperature` — optional; forwarded to the provider

## Output: IAsyncEnumerable\<ChatCompletionChunk\>

Yields zero or more chunks. Each chunk is an immutable record:

```csharp
public sealed record ChatCompletionChunk(
    string Content,
    string? FinishReason = null,
    string? Model = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    ToolCallDelta? ToolCallDelta = null);
```

### Chunk lifecycle

1. **Content chunks**: `Content` is non-empty, all other fields may be null.
2. **Finish chunk**: `FinishReason` is set, `Content` is typically empty.
3. **Usage chunk**: `PromptTokens` and/or `CompletionTokens` are set, `Content` is empty.
4. **Tool call chunks**: `ToolCallDelta` is populated, `Content` may be empty.

Some providers combine finish + usage into a single chunk (Anthropic, Cohere).
OpenAI/Azure may emit a separate usage-only chunk after the finish chunk.

## Error Conditions

| Condition | Behavior |
|-----------|----------|
| HTTP error (4xx, 5xx) | `LlmHttpRequestException` thrown before any chunk is yielded |
| Malformed JSON in SSE data line | Chunk is silently skipped; enumeration continues |
| Unknown SSE event type | Event is silently skipped |
| Empty stream (no data lines) | Enumeration completes with zero chunks |
| Cancellation token triggered | `OperationCanceledException` thrown; HTTP response disposed |

## Provider Implementation Matrix

| Provider | SSE Format | Stream Termination | Model Source | Usage Source |
|----------|-----------|-------------------|--------------|--------------|
| OpenAI (legacy) | choices/delta | `[DONE]` | chunk.Model | separate usage chunk |
| OpenAI (Responses API) | typed events | `[DONE]` | response.completed | response.completed |
| Azure OpenAI (legacy) | choices/delta | `[DONE]` | chunk.Model | separate usage chunk |
| Azure OpenAI (Responses API) | typed events | `[DONE]` | response.completed | response.completed |
| Azure AI Inference | choices/delta | `[DONE]` | chunk.Model | separate usage chunk |
| Anthropic | typed events | connection close | message_start | message_delta |
| Cohere | typed events | connection close | request.Model | message-end.billed_units |

## Transport: LlmHttpClient.PostStreamAsync

The shared SSE parser in `LlmHttpClient` (lines 183-281):

```csharp
public async IAsyncEnumerable<string> PostStreamAsync<TRequest>(
    string uri,
    TRequest payload,
    JsonElement? extraParameters = null,
    CancellationToken cancellationToken = default)
```

**Protocol**: Reads lines from the HTTP response stream. Skips blank lines,
`event:` lines, and non-`data:` lines. Strips the `data: ` prefix. Returns
`[DONE]` as a stream termination signal (yields nothing, breaks). Yields the
raw JSON string for all other data lines.

**Logging**: EventId 1003 (StreamStarted), 1004 (StreamChunkReceived at Debug level),
1005 (StreamCompleted with chunk count and completion kind).

**Tracing**: Creates an `Activity` span tagged with `cisharpai.stream=true`.

## Resilience: AddCisharpaiStreamingResilienceHandler

**File**: `src/Cisharpai/HttpClientBuilderExtensions.cs`

```csharp
public static IHttpClientBuilder AddCisharpaiStreamingResilienceHandler(
    this IHttpClientBuilder builder)
```

Sets `AttemptTimeout` and `TotalRequestTimeout` to `Timeout.InfiniteTimeSpan`.
Retry and circuit breaker remain at default Polly settings.

## Testing: FakeChatCompletionClient

**File**: `src/Cisharpai.Testing/FakeChatCompletionClient.cs`

- Enabled via `FakeChatFeatures.Streaming` flag
- `EnqueueStreamingResponse(IReadOnlyList<ChatCompletionChunk>)` — queue responses
- `DefaultStreamingResponse` — fallback when queue is empty
- `ReceivedStreamingRequests` — captured requests for assertions
- `StreamingCallCount` — number of streaming calls made
- `Reset()` — clears queues and captured requests
