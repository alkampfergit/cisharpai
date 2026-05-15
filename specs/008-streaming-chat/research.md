# Research: Streaming Chat Completions

## Technical Decisions

### Decision 1: Shared SSE line parser in LlmHttpClient

**Decision**: SSE line-level parsing (blank lines, `event:` lines, `data:` prefix
stripping, `[DONE]` detection) lives in `LlmHttpClient.PostStreamAsync`. Provider
clients receive raw JSON strings and handle their own deserialization.

**Rationale**: All providers use the same SSE wire format (RFC 8895 / EventSource).
The differences are in the JSON payloads, not the framing. Centralizing line parsing
avoids duplicating `StreamReader` + line-parsing logic in 5 providers.

**Alternatives considered**:
- Per-provider SSE parsing — rejected because it would duplicate ~50 lines of
  identical `StreamReader` + line-skipping logic 5 times.
- Shared generic deserialization with provider-specific `JsonConverter<T>` —
  rejected because it adds abstraction without benefit; each provider's event
  model is structurally different enough that switch-based parsing is clearer.

### Decision 2: Streaming as a method on the chat client, not a separate class

**Decision**: Each provider's `*ChatCompletionClient` directly implements
`IStreamingChatFeature` and registers itself via `features.Set<IStreamingChatFeature>(this)`.

**Rationale**: Streaming uses the same endpoint, authentication, model detection,
message mapping, and options as non-streaming requests. Extracting it into a
separate class would require duplicating or sharing all of that infrastructure.
Since `IStreamingChatFeature` is discovered via the Feature Collection pattern,
the consumer already uses a different interface — they don't need a different class.

**Alternatives considered**:
- Separate `*StreamingClient` class per provider — rejected because it would
  require either duplicating model detection/message mapping or extracting a
  shared base class, adding complexity for no consumer-facing benefit.

### Decision 3: Provider-specific stream event models (no shared base)

**Decision**: Each provider has its own stream event model classes
(`OpenAiStreamChunk`, `AnthropicStreamEvent`, `CohereStreamEvent`, etc.) with
no shared base class or interface.

**Rationale**: The SSE event structures are fundamentally different across providers:
- OpenAI/Azure use a `choices[].delta` array structure
- Anthropic uses `message_start`/`content_block_delta`/`message_delta` typed events
- Cohere uses `stream-start`/`content-delta`/`message-end` typed events

A shared base would need to be so generic it adds no value, or would require
a complex discriminated union pattern that obscures the simple switch-based
parsing in each provider.

**Alternatives considered**:
- Shared `IStreamEvent` interface — rejected because the mapping to
  `ChatCompletionChunk` is provider-specific and happens in the same method
  that deserializes the event.
- Generic `StreamEvent<T>` with provider-specific payload types — rejected
  as over-engineering for a stateless mapping.

### Decision 4: Dual-path streaming for OpenAI and Azure OpenAI (GPT-5)

**Decision**: The OpenAI and Azure OpenAI clients detect the model type and route
to either the legacy Chat Completions API stream or the Responses API stream.
GPT-5 models use `StreamResponsesApiAsync`; all others use `StreamLegacyChatAsync`.

**Rationale**: The Responses API (used by GPT-5) uses a completely different SSE
event format (`response.output_text.delta` / `response.completed`) and endpoint
(`/v1/responses` instead of `/v1/chat/completions`). The consumer should not need
to know which API is used underneath.

**Alternatives considered**:
- Force all models through one API — not possible because GPT-5 only works with
  the Responses API and older models only work with Chat Completions.
- Expose separate `StreamChatCompletionAsync` and `StreamResponsesAsync` methods
  — rejected because it breaks the unified abstraction principle.

### Decision 5: Infinite timeouts for streaming resilience handler

**Decision**: `AddCisharpaiStreamingResilienceHandler` sets both `AttemptTimeout`
and `TotalRequestTimeout` to `Timeout.InfiniteTimeSpan` while keeping retry and
circuit breaker at defaults.

**Rationale**: The standard resilience handler's 60s per-attempt and 90s total
timeouts are reasonable for request-response calls but would terminate any stream
running longer than 90 seconds. Streaming responses can legitimately run for
minutes (e.g., long-form content generation). The consumer should manage timeouts
via `CancellationToken` on the streaming enumeration instead.

**Alternatives considered**:
- Very long but finite timeouts (e.g., 30 minutes) — rejected because any
  fixed limit is arbitrary and will eventually be too short.
- No resilience handler for streaming — rejected because retry and circuit
  breaker are still valuable for the initial connection establishment.

### Decision 6: Silent skip for malformed JSON and unknown events

**Decision**: When a `data:` line cannot be deserialized or contains an unknown
event type, the provider silently continues to the next line instead of throwing.

**Rationale**: SSE streams can contain informational events (Anthropic's `ping`,
Cohere's `content-start`/`content-end`) that don't map to chunks. Providers may
also add new event types in the future. Throwing on unknown events would break
existing consumers when providers evolve their APIs.

**Alternatives considered**:
- Log a warning for unknown events — the `StreamChunkReceived` log at Debug level
  already captures every data line, so unknown events are visible in debug logs.
- Expose unknown events as chunks with empty content — rejected because it would
  pollute the consumer's chunk stream with noise.
