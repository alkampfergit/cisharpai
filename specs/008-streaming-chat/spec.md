# Feature Specification: Streaming Chat Completions

**Feature Branch**: `feature/gh-specify`

**Created**: 2026-05-15

**Status**: Retrospec (reverse-engineered from existing implementation)

**Input**: Reverse-engineered from `IStreamingChatFeature` and provider implementations

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Stream Chat Responses Token by Token (Priority: P1)

As a developer integrating with any LLM provider, I want to receive chat completion
tokens as they are generated so that I can display real-time typewriter-style output
to end users without waiting for the full response.

**Why this priority**: Streaming is the primary interaction mode for chat UIs. Without
it, users stare at a blank screen until the full response completes — unacceptable for
production chat applications.

**Independent Test**: Discover the streaming feature on any provider client, send a
simple prompt, and verify that multiple chunks arrive sequentially with non-empty content
that concatenates to a coherent response.

**Acceptance Scenarios**:

1. **Given** an OpenAI client configured with a valid model, **When** I call
   `GetChatCompletionStreamAsync` with a user message, **Then** I receive multiple
   `ChatCompletionChunk` objects with non-empty `Content` that concatenate to the
   full response text.
2. **Given** an Anthropic client, **When** I stream the same prompt, **Then** I
   receive chunks in the same unified `ChatCompletionChunk` format regardless of
   provider.
3. **Given** any provider client, **When** I call
   `client.Features.Get<IStreamingChatFeature>()`, **Then** it returns a non-null
   reference (same instance as the client).

---

### User Story 2 - Receive Finish Reason and Token Usage (Priority: P1)

As a developer, I want to know when a stream has completed and how many tokens were
consumed so that I can track costs and detect truncation.

**Why this priority**: Without finish reason and usage data, developers cannot
distinguish a complete response from a truncated one, nor can they monitor costs.

**Independent Test**: Stream a response and inspect the final chunk(s) for
`FinishReason` and token counts.

**Acceptance Scenarios**:

1. **Given** a completed stream from OpenAI, **When** the stream ends, **Then** the
   final chunk has `FinishReason` set to `"stop"` and a usage chunk contains
   `PromptTokens` and `CompletionTokens`.
2. **Given** a completed stream from Anthropic, **When** the stream ends, **Then**
   the final chunk has `FinishReason` set to `"end_turn"` with `PromptTokens` from
   `message_start` and `CompletionTokens` from `message_delta`.
3. **Given** a completed stream from Cohere, **When** the stream ends, **Then** the
   final chunk has `FinishReason` set to `"COMPLETE"` with token counts from
   `billed_units`.

---

### User Story 3 - Stream Tool Call Deltas (Priority: P2)

As a developer building agentic applications, I want to receive tool call arguments
incrementally during streaming so that I can begin processing tool invocations before
the full arguments are complete.

**Why this priority**: Tool calling during streaming is essential for responsive agent
loops but not required for basic chat UI.

**Independent Test**: Stream a response from OpenAI or Azure that includes tool calls,
verify `ToolCallDelta` is populated with incremental function name and arguments.

**Acceptance Scenarios**:

1. **Given** an OpenAI streaming response with tool calls, **When** chunks arrive,
   **Then** chunks contain `ToolCallDelta` with `Index`, `Id`, `FunctionName`, and
   `ArgumentsDelta` fields populated incrementally.
2. **Given** an Azure OpenAI streaming response with tool calls, **When** chunks
   arrive, **Then** `ToolCallDelta` follows the same structure as OpenAI.

---

### User Story 4 - Stream GPT-5 via Responses API (Priority: P2)

As a developer using GPT-5 models, I want streaming to work transparently through
the Responses API so that model upgrades do not require code changes.

**Why this priority**: The Responses API uses a different SSE event format than
the legacy Chat Completions API, but developers should not need to know which
API is used underneath.

**Independent Test**: Stream a request with model `"gpt-5"` and verify chunks arrive
in the standard `ChatCompletionChunk` format with content, finish reason, and usage.

**Acceptance Scenarios**:

1. **Given** an OpenAI client with model `"gpt-5"`, **When** I stream, **Then** the
   client automatically routes to the Responses API and yields
   `response.output_text.delta` events as `ChatCompletionChunk` objects.
2. **Given** an Azure OpenAI client with a GPT-5 deployment, **When** I stream,
   **Then** the client routes to the Azure Responses API endpoint with correct
   api-version and yields chunks in the unified format.
3. **Given** a `response.completed` event, **Then** the final chunk has
   `FinishReason` set to the response status, `Model`, `PromptTokens`, and
   `CompletionTokens`.

---

### User Story 5 - Stream Reasoning Models (Priority: P2)

As a developer using reasoning models (o1, o3, o4), I want streaming to use the
correct token parameter (`max_completion_tokens`) so that requests are not rejected.

**Why this priority**: Reasoning models reject `max_tokens` and require
`max_completion_tokens`. This must be handled automatically.

**Independent Test**: Stream a request with model `"o3"` on Azure OpenAI, capture
the request body, and verify it contains `max_completion_tokens` instead of `max_tokens`.

**Acceptance Scenarios**:

1. **Given** an Azure OpenAI client with a reasoning model deployment, **When** I
   stream with `MaxTokens: 500`, **Then** the wire request uses
   `max_completion_tokens: 500` and does not include `max_tokens`.
2. **Given** the same scenario, **When** the stream completes, **Then** chunks
   arrive in the standard unified format.

---

### User Story 6 - Cancel a Stream (Priority: P2)

As a developer, I want to cancel an in-progress stream via `CancellationToken` so
that I can implement timeouts and user-initiated cancellation.

**Why this priority**: Production systems must be able to abort long-running streams
without leaking resources.

**Independent Test**: Start a stream and cancel it mid-way; verify
`OperationCanceledException` is thrown.

**Acceptance Scenarios**:

1. **Given** an active stream, **When** the `CancellationToken` is cancelled,
   **Then** the enumeration throws `OperationCanceledException` and the HTTP
   response is disposed.

---

### User Story 7 - Configure Resilience for Streaming (Priority: P3)

As a developer, I want a dedicated resilience handler for streaming that removes
timeout constraints while keeping retry and circuit breaker policies.

**Why this priority**: The standard resilience handler's 60s/90s timeouts would
terminate long-running streams. This is a production-readiness concern.

**Independent Test**: Register `AddCisharpaiStreamingResilienceHandler` on an
`IHttpClientBuilder` and verify infinite timeouts are configured.

**Acceptance Scenarios**:

1. **Given** an HttpClient configured with `AddCisharpaiStreamingResilienceHandler`,
   **When** a stream runs for more than 90 seconds, **Then** it is not terminated
   by the resilience pipeline.

---

### User Story 8 - Fake Streaming in Unit Tests (Priority: P3)

As a downstream developer, I want to use `FakeChatCompletionClient` to mock streaming
responses in my unit tests without making real API calls.

**Why this priority**: Testability is a core principle but is downstream-facing, not
required for the streaming feature itself.

**Independent Test**: Create a `FakeChatCompletionClient` with
`FakeChatFeatures.Streaming`, enqueue a list of chunks, stream, and verify the
enqueued chunks are yielded.

**Acceptance Scenarios**:

1. **Given** a `FakeChatCompletionClient` with streaming enabled, **When** I enqueue
   chunks and call `GetChatCompletionStreamAsync`, **Then** I receive exactly those
   chunks in order.
2. **Given** a `FakeChatCompletionClient` with `FakeChatFeatures.None`, **When** I
   call `Features.Get<IStreamingChatFeature>()`, **Then** it returns null.

---

### Edge Cases

- What happens when the server sends an empty SSE stream (no data lines)? The
  enumeration completes with zero chunks yielded.
- What happens when the SSE stream contains unknown event types? They are silently
  skipped — only recognized event types produce chunks.
- What happens when a JSON chunk cannot be deserialized? The malformed chunk is
  skipped and enumeration continues with the next line.
- What happens when the HTTP request fails before streaming begins?
  `LlmHttpRequestException` is thrown before any chunks are yielded.
- What happens when the stream has no `[DONE]` sentinel (Anthropic, Cohere)? The
  stream ends naturally when the server closes the connection.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support streaming delivery of chat responses as an
  `IAsyncEnumerable<ChatCompletionChunk>`.
- **FR-002**: System MUST expose streaming as an optional feature discoverable via
  `IHasFeatures.Features.Get<IStreamingChatFeature>()`.
- **FR-003**: Each chunk MUST contain a content delta, with optional finish reason,
  model identifier, token usage, and tool call delta.
- **FR-004**: System MUST parse provider-specific SSE formats (OpenAI `[DONE]`
  sentinel, Anthropic event-based, Cohere event-based, Responses API event-based)
  into the unified `ChatCompletionChunk` model.
- **FR-005**: System MUST support cancellation via `CancellationToken` on the
  streaming enumeration.
- **FR-006**: System MUST propagate HTTP errors as exceptions before yielding any
  chunks (fail-fast on non-success status codes).
- **FR-007**: System MUST emit structured log entries and distributed tracing spans
  for streaming requests (EventIds 1003-1005).
- **FR-008**: System MUST provide a streaming-optimized resilience handler that
  disables per-attempt and total request timeouts.
- **FR-009**: The testing package MUST provide a fake streaming implementation with
  queue-based chunk injection and request capture.
- **FR-010**: System MUST support streaming for reasoning models by using the
  correct token parameter (`max_completion_tokens`).
- **FR-011**: System MUST transparently route GPT-5 models to the Responses API
  streaming format without requiring code changes from the consumer.
- **FR-012**: System MUST support streaming tool call deltas for providers that
  emit them (OpenAI, Azure OpenAI, Azure AI Inference).

### Key Entities

- **ChatCompletionChunk**: An immutable record representing a single streaming
  delta — content text, finish reason, model, token usage, and tool call delta.
- **ToolCallDelta**: An immutable record representing incremental tool call
  information — index, ID, function name, and arguments delta.
- **IStreamingChatFeature**: The feature interface that providers implement to
  advertise streaming capability.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 5 providers (OpenAI, Azure OpenAI, Azure AI Inference, Anthropic,
  Cohere) yield streaming chunks through the identical `IStreamingChatFeature`
  interface.
- **SC-002**: Content from streamed chunks concatenates to the same text as a
  non-streaming response for the same prompt.
- **SC-003**: Final chunks contain finish reason and token usage for all providers.
- **SC-004**: All 35+ unit tests across 5 provider test classes pass.
- **SC-005**: `FakeChatCompletionClient` supports streaming with queue-based
  response injection.
- **SC-006**: The streaming resilience handler allows streams of unlimited duration.

## Assumptions

- All target LLM providers support server-sent events (SSE) for streaming.
- The unified `ChatCompletionChunk` model is sufficient to represent all providers'
  streaming data — provider-specific metadata beyond content/usage/finish/tools is
  intentionally not surfaced.
- `LlmHttpClient.PostStreamAsync` handles the common SSE line protocol; providers
  handle their own JSON deserialization of the `data:` payloads.
- The library targets .NET 8.0 and .NET 10 with `IAsyncEnumerable<T>` support.
- Consumers are expected to handle `OperationCanceledException` for stream
  cancellation.

## Retrospec Metadata

**Generated**: 2026-05-15
**Source**: Reverse-engineered from existing implementation
**Analyzed files**: 27 files across 8 projects
**Reference implementation branch**: feature/gh-specify
