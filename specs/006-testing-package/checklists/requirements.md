# Requirements Checklist: Testing Package

## Functional Requirements

- [x] **FR-001**: Fake implementation of `IChatCompletionClient` that returns canned responses without network calls
  - Implemented: `FakeChatCompletionClient`; test: `GetChatCompletionAsync_ReturnsDefaultResponse`
- [x] **FR-002**: Response queuing (FIFO) and default responses for every feature method
  - Verified: `GetChatCompletionAsync_ReturnsQueuedResponseFirst`, `MultipleQueuedResponses_DrainInOrder`, `Streaming_SupportsQueuedResponses`
- [x] **FR-003**: Request capture for later assertion, organized by feature method
  - Verified: `CapturesReceivedRequests`, `ToolCalling_CapturesRequestAndOptions`, `JsonOutput_CapturesRequestAndOptions`, `GroundedChat_CapturesRequestAndOptions`
- [x] **FR-004**: `CallCount` property tracking total invocations across all methods
  - Verified: `CallCount_TracksAllMethods` (chat), `CallCount_TracksAllMethods` (embedding)
- [x] **FR-005**: `Reset()` method that clears all queues and captured requests
  - Verified: `Reset_ClearsEverything` (chat), `Reset_ClearsEverything` (embedding)
- [x] **FR-006**: `InvalidOperationException` when no response is configured
  - Verified: `GetChatCompletionAsync_ThrowsWhenNoResponseConfigured`, `GetEmbeddingsAsync_ThrowsWhenNoResponseConfigured`
- [x] **FR-007**: Fake implementation of `IEmbeddingClient` with queue/default/capture pattern
  - Implemented: `FakeEmbeddingClient`; test: `GetEmbeddingsAsync_ReturnsDefaultResponse`
- [x] **FR-008**: Selective feature registration via flags enums
  - Verified: `Features_AllRegisteredByDefault`, `Features_CanBeSelective`, `Features_None` (both chat and embedding)
- [x] **FR-009**: Static factory methods for common response objects with sensible defaults
  - Verified: 15 tests in `FakeResponsesTests` covering Chat, ChatError, ToolCall, ToolCalls, GroundedChat, StreamingChunks, Embedding, Embeddings, EmbeddingError
- [x] **FR-010**: DI extension methods that register fake clients as singletons and return the instance
  - Verified: `AddFakeChatCompletionClient_RegistersAndReturnsInstance`, `AddFakeEmbeddingClient_RegistersAndReturnsInstance`
- [x] **FR-011**: Fake chat client implements all feature interfaces
  - Verified: `Features_AllRegisteredByDefault` (chat) — IStreamingChatFeature, IToolCallingFeature, IJsonOutputFeature, IGroundedChatFeature
- [x] **FR-012**: Fake embedding client implements all feature interfaces
  - Verified: `Features_AllRegisteredByDefault` (embedding) — IImageEmbeddingFeature, IMultimodalEmbeddingFeature
- [x] **FR-013**: JSON output method falls back to `DefaultResponse`
  - Verified: `JsonOutput_FallsBackToDefaultResponse`
- [x] **FR-014**: Image embedding method falls back to `DefaultResponse`
  - Verified: `ImageEmbedding_FallsBackToDefaultResponse`
- [x] **FR-015**: Streaming method yields chunks asynchronously with cancellation support
  - Verified: `Streaming_ReturnsChunksInOrder`, `Streaming_SupportsQueuedResponses`

## User Stories

- [x] **US-1**: Fake chat completions in unit tests (P1)
- [x] **US-2**: Fake feature responses — tool calling, JSON output, streaming, grounded chat (P1)
- [x] **US-3**: Fake embedding client (P1)
- [x] **US-4**: Response factory methods (P2)
- [x] **US-5**: Feature opt-out for detection testing (P2)
- [x] **US-6**: DI registration for fake clients (P2)
- [x] **US-7**: Request capture and inspection (P2)

## Constitution Compliance

- [x] Unified Abstraction — Fakes implement the same interfaces as real providers
- [x] No Exceptions for API Errors — `FakeResponses.ChatError()` / `EmbeddingError()` for error simulation
- [x] Debuggability First — `FakeResponses` allows custom response construction
- [x] Immutability — All response DTOs are immutable records
- [x] Test-Driven Quality — 53 unit tests across 4 test classes
- [x] Multi-Target Compatibility — net8.0;net10.0
- [x] Documentation as Deliverable — `wiki/testing.md` comprehensive guide

## Documentation

- [x] `wiki/testing.md` — comprehensive guide with examples, API reference tables, testing patterns
- [x] `memories/project_overview.md` — testing package documented
