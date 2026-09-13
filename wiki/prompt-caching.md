# Prompt Caching

Prompt caching reduces cost and latency by reusing previously processed input tokens across requests. Cisharpai surfaces cache usage universally on the unified response and provides explicit cache control for providers that support it.

## How Caching Works Per Provider

| Provider | Caching Type | Control | Reporting |
|----------|-------------|---------|-----------|
| Anthropic | Explicit breakpoints | `IPromptCachingFeature` | `CachedInputTokens`, `CacheCreationInputTokens` |
| OpenAI | Automatic | None needed | `CachedInputTokens` |
| Azure OpenAI | Automatic | None needed | `CachedInputTokens` |
| Azure AI Inference | None | -- | -- |
| Cohere | None | -- | -- |

## Universal Cache Reporting

Every `ChatCompletionResponse` and `ChatCompletionChunk` carries two nullable fields:

| Field | Meaning |
|-------|---------|
| `PromptTokens` | Total input tokens (including cached and cache-creation tokens) |
| `CachedInputTokens` | Tokens served from cache (reduced cost) |
| `CacheCreationInputTokens` | Tokens written into cache this request (Anthropic-only) |

These fields are populated automatically — no feature discovery required. When a provider doesn't report caching, both cache fields are `null`.

`PromptTokens` is always the **true total** across all providers — it includes cached tokens, cache-creation tokens, and fresh tokens. This enables a universal formula:

```csharp
var response = await client.GetChatCompletionAsync(request);

if (response.CachedInputTokens is { } cached)
{
    var freshTokens = response.PromptTokens - cached;
    Console.WriteLine($"Cache hit: {cached} cached, {freshTokens} fresh");
}
```

For Anthropic, `PromptTokens = input_tokens + cache_read_input_tokens + cache_creation_input_tokens`. For OpenAI/Azure, `PromptTokens = prompt_tokens` (which already includes cached tokens as a subset).

### Streaming

Cache fields appear on the final `ChatCompletionChunk` (the one with `FinishReason` set):

```csharp
var feature = client.Features.Get<IStreamingChatFeature>()!;

await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
{
    if (chunk.CachedInputTokens is { } cached)
        Console.WriteLine($"Stream cache hit: {cached} tokens");
}
```

## Anthropic: Explicit Cache Control

Anthropic is the only provider that exposes cache breakpoints — points in the request where the provider should cache everything up to that position. Discover this via `IPromptCachingFeature`:

```csharp
var cachingFeature = client.Features.Get<IPromptCachingFeature>();
if (cachingFeature is null)
{
    // Provider uses automatic caching or doesn't support it
    // Cache usage still reported on the response
    return;
}
```

### PromptCachingOptions

`PromptCachingOptions` controls where `cache_control: {"type": "ephemeral"}` breakpoints are placed:

| Property | Type | Description |
|----------|------|-------------|
| `CacheSystemMessage` | `bool` | Place a breakpoint on the system message |
| `MessageBreakpoints` | `IReadOnlyList<int>` | Zero-based message indices to cache up to |
| `ToolBreakpoints` | `IReadOnlyList<int>` | Zero-based tool definition indices to cache |

### Example: Cache System Prompt + Document Context

```csharp
var request = new ChatCompletionRequest(
    Messages:
    [
        new LlmMessage(LlmRole.System, longSystemPrompt),
        new LlmMessage(LlmRole.User, documentContext),  // index 0 (system excluded)
        new LlmMessage(LlmRole.User, userQuestion)       // index 1
    ],
    Model: "claude-sonnet-4-20250514");

var cachingOptions = new PromptCachingOptions
{
    CacheSystemMessage = true,     // Cache the system prompt
    MessageBreakpoints = [0]       // Cache through the document context
};

var response = await cachingFeature.GetChatCompletionWithCachingAsync(
    request, cachingOptions);

// First call: CacheCreationInputTokens > 0, CachedInputTokens = null
// Second call: CachedInputTokens > 0, CacheCreationInputTokens = null
Console.WriteLine($"Created: {response.CacheCreationInputTokens}");
Console.WriteLine($"Read: {response.CachedInputTokens}");
```

### Message Breakpoint Indices

The `MessageBreakpoints` indices refer to positions in the **provider message list** (after the system message is extracted). The Anthropic API handles system messages separately, so index 0 in `MessageBreakpoints` is the first non-system message.

Out-of-range indices are silently ignored — this prevents errors if the message list changes between calls.

### RAG Use Case: Grounded Chat with Caching

The primary use case for prompt caching is RAG: cache the document corpus across conversation turns while the user question changes. Use `GetGroundedChatCompletionWithCachingAsync` to compose document grounding with cache breakpoints in a single call:

```csharp
var cachingFeature = client.Features.Get<IPromptCachingFeature>()!;

var groundedOptions = new GroundedChatOptions(documents);
var cachingOptions = new PromptCachingOptions
{
    CacheSystemMessage = true,
    MessageBreakpoints = [0]  // Cache the message containing injected documents
};

var response = await cachingFeature.GetGroundedChatCompletionWithCachingAsync(
    request, groundedOptions, cachingOptions);

// response.Citations contains source references
// response.ChatCompletion.CachedInputTokens shows cache hits
```

### Tool Calling with Caching

Use `GetChatCompletionWithToolsAndCachingAsync` to cache tool definitions alongside messages:

```csharp
var cachingFeature = client.Features.Get<IPromptCachingFeature>()!;

var toolOptions = new ToolCallingOptions(tools);
var cachingOptions = new PromptCachingOptions
{
    CacheSystemMessage = true,
    ToolBreakpoints = [tools.Count - 1]  // Cache all tool definitions
};

var response = await cachingFeature.GetChatCompletionWithToolsAndCachingAsync(
    request, toolOptions, cachingOptions);
```

## OpenAI / Azure OpenAI: Automatic Caching

OpenAI and Azure OpenAI cache prompts automatically — there is no control surface. Simply check the response:

```csharp
var response = await client.GetChatCompletionAsync(request);

if (response.CachedInputTokens.HasValue)
    Console.WriteLine($"OpenAI cached {response.CachedInputTokens} tokens");
```

`CacheCreationInputTokens` is always `null` for OpenAI/Azure OpenAI since caching is transparent.

## Testing

Use `FakeResponses.CachedChat` to create responses with cache fields. Both `cachedInputTokens` and `cacheCreationInputTokens` are optional — omit `cachedInputTokens` to simulate a first-call (cache-creation-only) response:

```csharp
var fake = new FakeChatCompletionClient();

// Cache hit response (second+ call)
fake.DefaultPromptCachingResponse = FakeResponses.CachedChat(
    "cached response",
    cachedInputTokens: 500,
    cacheCreationInputTokens: 200);

// Cache creation response (first call)
fake.EnqueuePromptCachingResponse(FakeResponses.CachedChat(
    "first call", cacheCreationInputTokens: 1000));

var feature = fake.Features.Get<IPromptCachingFeature>()!;
var response = await feature.GetChatCompletionWithCachingAsync(request, options);

Assert.That(response.CachedInputTokens, Is.EqualTo(500));
```

The `FakeChatCompletionClient` registers `IPromptCachingFeature` by default (included in `FakeChatFeatures.All`). Disable it with:

```csharp
var fake = new FakeChatCompletionClient(
    FakeChatFeatures.All & ~FakeChatFeatures.PromptCaching);
```
