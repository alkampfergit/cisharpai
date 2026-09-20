# Streaming Chat Completions

Cisharpai supports streaming chat completions via the `IStreamingChatFeature` optional feature. This lets you receive tokens as they are generated, enabling real-time typewriter-style output.

## Quick Start

```csharp
using Cisharpai.Features.Chat;
using Cisharpai.Models;

IChatCompletionClient client = /* any provider */;

// Check streaming support
var streamFeature = client.Features.Get<IStreamingChatFeature>();
if (streamFeature is null)
{
    Console.WriteLine("Streaming not supported.");
    return;
}

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Write a poem about the sea")],
    Model: "gpt-4.1-nano",
    MaxTokens: 500);

// Stream tokens as they arrive
await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
{
    if (!string.IsNullOrEmpty(chunk.Content))
        Console.Write(chunk.Content);
}
Console.WriteLine();
```

## The ChatCompletionChunk Model

Each chunk yielded from the stream contains:

| Property | Type | Description |
|----------|------|-------------|
| `Content` | `string` | Token text for this chunk (may be empty) |
| `FinishReason` | `string?` | Set on the final chunk (`"stop"`, `"end_turn"`, `"COMPLETE"`) |
| `Model` | `string?` | Model identifier (may be populated from first chunk) |
| `PromptTokens` | `int?` | Input token count (usually in the final usage chunk) |
| `CompletionTokens` | `int?` | Output token count (usually in the final usage chunk) |
| `ToolCallDelta` | `ToolCallDelta?` | Tool call delta for streaming tool calls (OpenAI/Azure) |

## Accumulating Content

```csharp
var sb = new System.Text.StringBuilder();
string? finishReason = null;
int? promptTokens = null;
int? completionTokens = null;

await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
{
    sb.Append(chunk.Content);

    if (chunk.FinishReason is not null)
        finishReason = chunk.FinishReason;

    if (chunk.PromptTokens.HasValue)
        promptTokens = chunk.PromptTokens;

    if (chunk.CompletionTokens.HasValue)
        completionTokens = chunk.CompletionTokens;
}

string fullResponse = sb.ToString();
Console.WriteLine($"Response: {fullResponse}");
Console.WriteLine($"Finish: {finishReason}, Tokens: {promptTokens}+{completionTokens}");
```

## Provider Support

All chat clients expose `IStreamingChatFeature`:

| Provider | Support | Notes |
|----------|---------|-------|
| OpenAI | Yes | Chat Completions API uses `[DONE]`; Responses API (GPT-5) uses `response.completed` |
| Azure OpenAI | Yes | Same SSE format as OpenAI; `[DONE]` terminates the stream |
| Azure AI Inference | Yes | Same SSE format as OpenAI; `[DONE]` terminates the stream |
| Anthropic | Yes | Event-based SSE; no `[DONE]` sentinel; stream ends naturally |
| Cohere | Yes | Event-based SSE; `content-delta` and `message-end` events |

## Feature Discovery

```csharp
IChatCompletionClient client = /* any provider */;

var streamFeature = client.Features.Get<IStreamingChatFeature>();
if (streamFeature is not null)
{
    // Streaming is supported by this client
    await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
    {
        // ...
    }
}
```

## Cancellation

Streaming supports cancellation via `CancellationToken`:

```csharp
using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30-second timeout

try
{
    await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request, cts.Token))
    {
        Console.Write(chunk.Content);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("\n[Stream cancelled]");
}
```

## Provider-Specific Notes

### OpenAI & Azure OpenAI
- Legacy models (GPT-4, GPT-4o): uses Chat Completions API with `stream: true`
- GPT-5 (Responses API): uses `response.output_text.delta` and `response.completed` events
- Reasoning models (o1/o3/o4): streaming uses `max_completion_tokens`
- Usage data available in the final chunk when `stream_options.include_usage: true`

### Anthropic
- Streaming uses `stream: true` in the request body
- Events: `message_start` (model + input tokens), `content_block_delta` (text), `message_delta` (finish + output tokens)
- Stream ends naturally (no `[DONE]`)

### Cohere
- Streaming uses `stream: true` in the request body
- Events: `stream-start`, `content-delta`, `message-end`
- `FinishReason` values are uppercase: `COMPLETE`, `MAX_TOKENS`

## Streaming Resilience

For production use, configure the HttpClient with infinite timeouts to prevent long streams from being cut off:

```csharp
services.AddHttpClient<IChatCompletionClient>("streaming")
    .AddCisharpaiStreamingResilienceHandler();
```

The standard `AddCisharpaiResilienceHandler()` sets 60s/90s timeouts which would terminate streams longer than those limits. `AddCisharpaiStreamingResilienceHandler()` removes these timeout constraints while keeping retry and circuit-breaker policies.

## Error Handling

If the HTTP request fails, `GetChatCompletionStreamAsync` throws `LlmHttpRequestException` before yielding any chunks. Handle it with try/catch:

```csharp
try
{
    await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
    {
        Console.Write(chunk.Content);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Stream failed: {ex.Message}");
}
```

## See Also

- [Provider Features Matrix](provider-features.md) — full feature support table
- [Feature Extensions](feature-extensions.md) — Feature Collection pattern documentation
