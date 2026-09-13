# Spec: Prompt caching support across providers

**Issue**: #41 | **Branch**: `feature/041-prompt-caching` | **Date**: 2026-09-13

## Problem

LLM providers offer prompt caching to reduce cost and latency by reusing previously processed input tokens. Anthropic exposes explicit cache breakpoints; OpenAI and Azure OpenAI cache automatically. Cisharpai had no way to report cache usage or control cache placement.

## Changes

### Universal cache reporting

Two nullable fields added to `ChatCompletionResponse` and `ChatCompletionChunk`:

- `int? CachedInputTokens` — tokens served from cache (reduced cost).
- `int? CacheCreationInputTokens` — tokens written to cache this request (Anthropic only).

Both are `null` when the provider doesn't report caching or when no cache activity occurred. OpenAI/Azure OpenAI zero values are suppressed to `null` (`cachedTokens > 0 ? cachedTokens : null`).

### Anthropic: explicit cache control via `IPromptCachingFeature`

- New `IPromptCachingFeature` interface in `Cisharpai.Features.Chat` with `GetChatCompletionWithCachingAsync(ChatCompletionRequest, PromptCachingOptions, CancellationToken)`.
- New `PromptCachingOptions` record: `CacheSystemMessage` (bool), `MessageBreakpoints` (IReadOnlyList<int>), `ToolBreakpoints` (IReadOnlyList<int>).
- `AnthropicChatCompletionClient` registers the feature and implements `ApplyCacheBreakpoints`, which injects `cache_control: {"type": "ephemeral"}` on system messages, message content blocks, and tool definitions at the specified indices. Out-of-range indices are silently ignored.
- `AnthropicChatRequest.System` changed from `string?` to `object?` to support both plain string and `List<AnthropicSystemBlock>` (same pattern as `AnthropicMessage.Content`).
- `AnthropicUsage` gains `CacheCreationInputTokens` and `CacheReadInputTokens`.
- Streaming: cache token counts extracted from `message_start` usage and emitted on `message_delta`.

### OpenAI: report-only (automatic caching)

- `OpenAiPromptTokensDetails` with `CachedTokens` added to `OpenAiUsage`.
- `OpenAiInputTokensDetails` with `CachedTokens` added to `OpenAiResponsesUsage`.
- All mapping methods (`MapChatResponse`, `MapToolCallingResponse`, `ExecuteResponsesApiAsync`, `MapGroundedChatResponse`) and both streaming paths pass `CachedInputTokens`.
- No `IPromptCachingFeature` registered — OpenAI caching is automatic.

### Azure OpenAI: report-only (automatic caching)

- Mirrors OpenAI: `AzureOpenAiPromptTokensDetails` and `AzureOpenAiInputTokensDetails`.
- All mapping methods and streaming paths pass `CachedInputTokens`.
- No `IPromptCachingFeature` registered.

### Azure AI Inference / Cohere: no support

Cache fields are always `null` — these providers do not report caching.

### Testing support

- `FakeChatFeatures.PromptCaching` flag (included in `All`).
- `FakeChatCompletionClient` implements `IPromptCachingFeature` with queue, default response, and request capture.
- `FakeResponses.CachedChat(content, cachedInputTokens, cacheCreationInputTokens?)` factory.

## Design decisions

1. **Universal reporting, feature-gated control** — cache token counts live on the shared DTOs since every provider can report them. Control (`IPromptCachingFeature`) is only registered on Anthropic because it's the only provider with explicit breakpoints. No no-op control surface elsewhere.
2. **Index-based breakpoints** — `MessageBreakpoints` uses zero-based indices into the provider message list (post system-message extraction). This keeps `cache_control` out of core types (`LlmMessage`/`ContentParts`), following the Feature Collection Pattern principle that optional, provider-specific billing details shouldn't leak into shared immutable DTOs.
3. **`object?` for System field** — `AnthropicChatRequest.System` changed to `object?` (from `string?`) to hold either a plain string or `List<AnthropicSystemBlock>` with cache control. This matches the existing `AnthropicMessage.Content` pattern.
4. **Zero-value suppression** — OpenAI reports `cached_tokens: 0` when no caching occurs. Mapping to `null` keeps the semantics clean: `null` means "no cache activity", not "zero cached tokens".
