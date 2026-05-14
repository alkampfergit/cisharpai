# Feature Specification: Testing Package

**Feature Branch**: `006-testing-package`

**Created**: 2026-05-14

**Status**: Complete (retrospec)

**Input**: Reverse-engineered from `Cisharpai.Testing` project

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fake Chat Completions in Unit Tests (Priority: P1)

As a developer building an application on top of Cisharpai, I want to substitute the real `IChatCompletionClient` with a fake that returns canned responses so that my unit tests run without API keys, network access, or cost.

**Why this priority**: This is the core value proposition — without the ability to fake chat completions, every test would require live API calls.

**Independent Test**: Create a `FakeChatCompletionClient`, set a `DefaultResponse`, call `GetChatCompletionAsync`, and verify the canned content is returned.

**Acceptance Scenarios**:

1. **Given** a `FakeChatCompletionClient` with a default response configured, **When** `GetChatCompletionAsync` is called, **Then** the default response is returned and the request is captured.
2. **Given** a `FakeChatCompletionClient` with enqueued responses, **When** multiple calls are made, **Then** queued responses are returned in FIFO order, falling back to the default after the queue is drained.
3. **Given** a `FakeChatCompletionClient` with no response configured, **When** any method is called, **Then** an `InvalidOperationException` is thrown with a descriptive message.

---

### User Story 2 - Fake Feature Responses (Tool Calling, JSON Output, Streaming, Grounded Chat) (Priority: P1)

As a developer using Cisharpai features beyond basic chat (tool calling, JSON output, streaming, grounded chat), I want each feature to have its own canned-response queue and default so I can test feature-specific code paths.

**Why this priority**: Features like tool calling and streaming have distinct response types; a single fake queue cannot serve all feature methods.

**Independent Test**: Configure `DefaultToolCallingResponse`, call `GetChatCompletionWithToolsAsync`, and verify tool calls are returned. Repeat for each feature.

**Acceptance Scenarios**:

1. **Given** a fake client with `DefaultToolCallingResponse`, **When** `GetChatCompletionWithToolsAsync` is called, **Then** the tool calling response is returned and the request+options are captured.
2. **Given** a fake client with `DefaultStreamingResponse`, **When** `GetChatCompletionStreamAsync` is called, **Then** chunks are yielded in order with the last chunk having `FinishReason="stop"`.
3. **Given** a fake client with `DefaultJsonOutputResponse` not set but `DefaultResponse` set, **When** `GetChatCompletionWithJsonOutputAsync` is called, **Then** it falls back to `DefaultResponse`.
4. **Given** a fake client with `DefaultGroundedChatResponse`, **When** `GetGroundedChatCompletionAsync` is called, **Then** the grounded response with citations is returned.

---

### User Story 3 - Fake Embedding Client (Priority: P1)

As a developer using embedding features, I want a `FakeEmbeddingClient` that supports text, image, and multimodal embedding methods with the same queue/default/capture pattern as the chat client.

**Why this priority**: Embedding functionality is a core Cisharpai capability; tests must be able to fake embedding responses.

**Independent Test**: Create a `FakeEmbeddingClient`, set `DefaultResponse`, call `GetEmbeddingsAsync`, and verify the canned embeddings are returned.

**Acceptance Scenarios**:

1. **Given** a `FakeEmbeddingClient` with a default response, **When** `GetEmbeddingsAsync` is called, **Then** the canned embedding vectors are returned.
2. **Given** a `FakeEmbeddingClient` with `DefaultImageResponse`, **When** `GetImageEmbeddingAsync` is called, **Then** the image embedding response is returned and the image path is captured.
3. **Given** a `FakeEmbeddingClient` with no image default but a `DefaultResponse`, **When** `GetImageEmbeddingAsync` is called, **Then** it falls back to `DefaultResponse`.

---

### User Story 4 - Response Factory Methods (Priority: P2)

As a developer writing tests, I want concise factory methods for creating common response objects (chat, error, tool call, streaming chunks, embeddings) so I do not need to construct complex DTOs manually.

**Why this priority**: Reduces test boilerplate — without factories, every test needs 5+ lines to create a response object.

**Independent Test**: Call `FakeResponses.Chat("content")` and verify the returned response has sensible defaults for model, token counts, and success status.

**Acceptance Scenarios**:

1. **Given** the `FakeResponses.Chat` factory, **When** called with content, **Then** it returns a successful response with model "fake-model" and default token counts.
2. **Given** the `FakeResponses.ToolCall` factory, **When** called with function name and JSON arguments, **Then** it returns a `ToolCallingResponse` with parsed arguments.
3. **Given** the `FakeResponses.StreamingChunks` factory, **When** called with text segments, **Then** only the last chunk has `FinishReason="stop"`.
4. **Given** the `FakeResponses.Embedding` factory, **When** called with no arguments, **Then** it returns a default vector `[0.1, 0.2, 0.3]`.

---

### User Story 5 - Feature Opt-Out for Detection Testing (Priority: P2)

As a developer testing feature-detection code paths, I want to control which optional features the fake client exposes so I can verify my application gracefully handles missing features.

**Why this priority**: Real providers have varying feature support; tests must simulate this variance.

**Independent Test**: Create a `FakeChatCompletionClient(FakeChatFeatures.None)` and verify `Features.Get<IStreamingChatFeature>()` returns null.

**Acceptance Scenarios**:

1. **Given** `FakeChatCompletionClient(FakeChatFeatures.All)`, **When** any feature is queried, **Then** it is available.
2. **Given** `FakeChatCompletionClient(FakeChatFeatures.None)`, **When** any feature is queried, **Then** it returns null.
3. **Given** `FakeChatCompletionClient(FakeChatFeatures.Streaming | FakeChatFeatures.ToolCalling)`, **When** features are queried, **Then** only Streaming and ToolCalling are available.

---

### User Story 6 - DI Registration for Fake Clients (Priority: P2)

As a developer using `Microsoft.Extensions.DependencyInjection` in my application, I want DI extension methods that register fake clients as singletons and return the instance for setup and assertions.

**Why this priority**: Integration with the standard DI container matches how real Cisharpai clients are registered.

**Independent Test**: Call `services.AddFakeChatCompletionClient()`, resolve `IChatCompletionClient` from the container, and verify it is the fake instance.

**Acceptance Scenarios**:

1. **Given** `AddFakeChatCompletionClient()` on a `ServiceCollection`, **When** `IChatCompletionClient` is resolved, **Then** it is the fake instance and supports feature discovery.
2. **Given** `AddFakeChatCompletionClient(FakeChatFeatures.Streaming)`, **When** resolved, **Then** only streaming is available.
3. **Given** `AddFakeEmbeddingClient()`, **When** `IEmbeddingClient` is resolved, **Then** it is the fake with all embedding features enabled.

---

### User Story 7 - Request Capture and Inspection (Priority: P2)

As a developer, I want the fake clients to capture all received requests so I can assert on what was sent (messages, options, parameters) without inspecting HTTP traffic.

**Why this priority**: Verifying that application code sends the correct prompts and options is a key testing pattern.

**Independent Test**: Call `GetChatCompletionAsync` with a specific request, then assert on `ReceivedRequests[0].Messages`.

**Acceptance Scenarios**:

1. **Given** a fake chat client with a default response, **When** multiple calls are made, **Then** `ReceivedRequests` contains each request in order.
2. **Given** a fake chat client, **When** tool calling, JSON output, grounded chat, and streaming calls are made, **Then** `CallCount` reflects the total across all methods.
3. **Given** a fake chat client with captured requests, **When** `Reset()` is called, **Then** all captured requests and queued responses are cleared.

---

### Edge Cases

- What happens when the response queue is exhausted and no default is set? → `InvalidOperationException` with descriptive message.
- What happens when `Reset()` is called mid-test? → All queues and capture lists are cleared; defaults remain.
- What happens when `DefaultJsonOutputResponse` is null? → Falls back to `DefaultResponse`.
- What happens when `DefaultImageResponse` is null? → Falls back to `DefaultResponse`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a fake implementation of `IChatCompletionClient` that returns canned responses without network calls
- **FR-002**: System MUST support response queuing (FIFO) and default responses for every feature method
- **FR-003**: System MUST capture all received requests for later assertion, organized by feature method
- **FR-004**: System MUST provide a `CallCount` property tracking total invocations across all methods
- **FR-005**: System MUST provide a `Reset()` method that clears all queues and captured requests
- **FR-006**: System MUST throw `InvalidOperationException` when no response is configured (no queue item, no default)
- **FR-007**: System MUST provide a fake implementation of `IEmbeddingClient` with the same queue/default/capture pattern
- **FR-008**: System MUST support selective feature registration via flags enums (`FakeChatFeatures`, `FakeEmbeddingFeatures`)
- **FR-009**: System MUST provide static factory methods for creating common response objects with sensible defaults
- **FR-010**: System MUST provide DI extension methods that register fake clients as singletons and return the instance
- **FR-011**: Fake chat client MUST implement all feature interfaces: `IStreamingChatFeature`, `IToolCallingFeature`, `IJsonOutputFeature`, `IGroundedChatFeature`
- **FR-012**: Fake embedding client MUST implement all feature interfaces: `IImageEmbeddingFeature`, `IMultimodalEmbeddingFeature`
- **FR-013**: JSON output method MUST fall back to `DefaultResponse` when `DefaultJsonOutputResponse` is null
- **FR-014**: Image embedding method MUST fall back to `DefaultResponse` when `DefaultImageResponse` is null
- **FR-015**: Streaming method MUST yield chunks asynchronously and support cancellation

### Key Entities

- **FakeChatCompletionClient**: In-memory chat client with queue/default/capture for all chat features
- **FakeEmbeddingClient**: In-memory embedding client with queue/default/capture for all embedding features
- **FakeResponses**: Static factory for creating response objects with sensible defaults
- **FakeChatFeatures**: Flags enum controlling which chat features are registered
- **FakeEmbeddingFeatures**: Flags enum controlling which embedding features are registered
- **FakeServiceCollectionExtensions**: DI registration helpers returning the fake instance

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All Cisharpai feature interfaces can be faked without network access
- **SC-002**: Test authors need at most 3 lines to set up a fake client with a canned response
- **SC-003**: Every request sent to a fake client can be inspected after the call
- **SC-004**: Feature detection code paths can be tested by controlling which features the fake exposes
- **SC-005**: DI-registered fakes are indistinguishable from real clients at the interface level

## Assumptions

- Consumers use `Microsoft.Extensions.DependencyInjection` for DI registration
- The testing package has no runtime dependency beyond the core `Cisharpai` project and DI abstractions
- Fake clients are single-threaded within a test; thread-safety of queues is not a concern
- The `FakeResponses` factory uses a constant `"fake-model"` as the default model name

## Retrospec Metadata

**Generated**: 2026-05-14
**Source**: Reverse-engineered from existing implementation
**Analyzed files**: 10 files across 2 projects
**Reference implementation branch**: feature/gh-specify
