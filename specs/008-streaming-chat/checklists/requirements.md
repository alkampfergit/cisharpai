# Requirements Checklist: Streaming Chat Completions

## Functional Requirements

- [x] **FR-001**: System supports streaming delivery via `IAsyncEnumerable<ChatCompletionChunk>`
  — Implemented in all 5 provider clients' `GetChatCompletionStreamAsync` methods.

- [x] **FR-002**: Streaming exposed as optional feature via `IHasFeatures.Features.Get<IStreamingChatFeature>()`
  — Each client registers with `features.Set<IStreamingChatFeature>(this)` in constructor.

- [x] **FR-003**: Each chunk contains content delta with optional finish reason, model, usage, tool call delta
  — `ChatCompletionChunk` record has all fields. Verified by unit tests across all providers.

- [x] **FR-004**: Provider-specific SSE formats parsed into unified model
  — OpenAI/Azure: `[DONE]` + choices/delta. Anthropic: event-typed SSE. Cohere: event-typed SSE. All tested.

- [x] **FR-005**: Cancellation via `CancellationToken`
  — `[EnumeratorCancellation]` attribute on all implementations. `CancellationToken.ThrowIfCancellationRequested()` in `PostStreamAsync`. Fake client also supports cancellation.

- [x] **FR-006**: HTTP errors propagated as exceptions before any chunks
  — `LlmHttpClient.PostStreamAsync` calls `EnsureSuccessOrThrowAsync` before reading the stream body.

- [x] **FR-007**: Structured logging and tracing for streaming
  — EventIds 1003 (StreamStarted), 1004 (StreamChunkReceived), 1005 (StreamCompleted). Activity span with `cisharpai.stream=true` tag.

- [x] **FR-008**: Streaming-optimized resilience handler
  — `AddCisharpaiStreamingResilienceHandler` sets infinite timeouts. Implemented in `HttpClientBuilderExtensions.cs`.

- [x] **FR-009**: Fake streaming in testing package
  — `FakeChatCompletionClient` supports `FakeChatFeatures.Streaming` with `EnqueueStreamingResponse`, `DefaultStreamingResponse`, `ReceivedStreamingRequests`, `StreamingCallCount`.

- [x] **FR-010**: Reasoning model support with `max_completion_tokens`
  — Azure OpenAI and Azure AI Inference detect reasoning models and use `AzureOpenAiReasoningChatRequest` / `AzureAiInferenceReasoningChatRequest` with `MaxCompletionTokens`. Verified by `Reasoning_Model_Uses_MaxCompletionTokens` test.

- [x] **FR-011**: Transparent GPT-5 routing to Responses API
  — OpenAI detects `OpenAiModelType.Gpt5` and routes to `StreamResponsesApiAsync`. Azure OpenAI detects `AzureOpenAiModelType.Gpt5` similarly. Verified by `Responses_Api_Streams_Text_For_Gpt5` and `Responses_Api_Final_Chunk_Has_Status` tests.

- [x] **FR-012**: Streaming tool call deltas
  — `ToolCallingHelper.MapStreamToolCallDelta` extracts tool call deltas from OpenAI, Azure OpenAI, and Azure AI Inference streaming chunks. `ToolCallDelta` record carries Index, Id, FunctionName, ArgumentsDelta.

## Constitution Compliance

- [x] **I. Unified Abstraction** — Same `IStreamingChatFeature` interface for all providers.
- [x] **II. No Exceptions for API Errors** — HTTP errors throw before streaming (network-level). Stream content never throws.
- [x] **III. Debuggability First** — `ExtraParameters` deep-merged into streaming requests. Full structured logging.
- [x] **IV. Immutability** — `ChatCompletionChunk` and `ToolCallDelta` are immutable records.
- [x] **V. Test-Driven Quality** — 35+ unit tests, 3 integration test files, fake client support.
- [x] **VI. Multi-Target Compatibility** — All projects target .NET 8.0 and .NET 10.
- [x] **VII. Documentation as Deliverable** — `wiki/streaming.md` (174 lines) fully documents the feature.

## User Stories Coverage

- [x] **US1** — Stream Chat Responses Token by Token: All 5 providers tested.
- [x] **US2** — Finish Reason and Token Usage: All providers emit finish reason and usage.
- [x] **US3** — Stream Tool Call Deltas: OpenAI/Azure providers support tool call deltas.
- [x] **US4** — GPT-5 via Responses API: OpenAI and Azure OpenAI tested.
- [x] **US5** — Reasoning Models: Azure OpenAI `max_completion_tokens` tested.
- [x] **US6** — Cancel a Stream: CancellationToken support on all implementations.
- [x] **US7** — Streaming Resilience Handler: `AddCisharpaiStreamingResilienceHandler` implemented.
- [x] **US8** — Fake Streaming in Unit Tests: `FakeChatCompletionClient.Streaming` tested.

## Test Coverage Summary

| Provider | Test File | Test Count | Scenarios |
|----------|-----------|------------|-----------|
| OpenAI | `OpenAiStreamingTests.cs` | 7 | Legacy text, legacy finish/usage, Responses API text/status, feature discovery, model, request body |
| Azure OpenAI | `AzureOpenAiStreamingTests.cs` | 7 | Text, finish, usage, request body, feature discovery, reasoning model |
| Azure AI Inference | `AzureAiInferenceStreamingTests.cs` | 6 | Text, finish, usage, request body, feature discovery, model from options |
| Anthropic | `AnthropicStreamingTests.cs` | 7 | Text, stop reason, input tokens, request body, feature discovery, model from message_start, empty stream |
| Cohere | `CohereStreamingTests.cs` | 8 | Text, finish, usage, request body, model, feature discovery, unknown events, empty stream |
