# Streaming

## Quick Start

```csharp
var streamFeature = client.Features.Get<IStreamingChatFeature>();

await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
{
    Console.Write(chunk.Content);
}
Console.WriteLine();
```

## ChatCompletionChunk Properties

- `Content` — Token text (may be empty string)
- `FinishReason` — Set on final chunk: `"stop"`, `"end_turn"`, `"COMPLETE"`
- `Model` — Model name
- `PromptTokens` — Input tokens (final chunk)
- `CompletionTokens` — Output tokens (final chunk)
- `ToolCallDelta` — For streaming tool calls (OpenAI/Azure)

## Cancellation

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request, cts.Token))
{
    Console.Write(chunk.Content);
}
```

## Streaming Resilience

The standard `AddCisharpaiResilienceHandler()` uses 60 second per-attempt and 90 second total timeouts. Those limits can cut off long-running SSE streams.

For long-running streams, configure the streaming `HttpClient` registration with infinite resilience timeouts:

```csharp
services.AddHttpClient("cisharpai-streaming")
    .AddCisharpaiStreamingResilienceHandler();
```

`AddCisharpaiStreamingResilienceHandler()` keeps retry and circuit-breaker behavior while removing the standard request timeout limits.

## Collecting Full Response

```csharp
var sb = new StringBuilder();
int? promptTokens = null, completionTokens = null;

await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
{
    if (!string.IsNullOrEmpty(chunk.Content))
        sb.Append(chunk.Content);

    if (chunk.PromptTokens.HasValue) promptTokens = chunk.PromptTokens;
    if (chunk.CompletionTokens.HasValue) completionTokens = chunk.CompletionTokens;
}

var fullResponse = sb.ToString();
```

## Error Handling

Errors throw `LlmHttpRequestException` before any chunks yield:

```csharp
try
{
    await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
        Console.Write(chunk.Content);
}
catch (LlmHttpRequestException ex)
{
    Console.WriteLine($"Stream error: {ex.Message}");
}
```

## Provider Differences

| Provider | Termination | Events |
|----------|------------|--------|
| OpenAI | `[DONE]` sentinel | `data:` lines |
| OpenAI (GPT-5) | `response.completed` | Responses API events |
| Azure OpenAI | `[DONE]` sentinel | `data:` lines |
| Azure AI Inference | `[DONE]` sentinel | `data:` lines |
| Anthropic | `message_stop` event | `message_start`, `content_block_delta`, `message_delta` |
| Cohere | `message-end` event | `content-delta`, `message-end` |

All differences are normalized into `ChatCompletionChunk` by Cisharpai.
