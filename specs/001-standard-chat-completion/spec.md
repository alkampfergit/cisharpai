# Feature Specification: Standard Chat Completion

**Feature Branch**: `feature/gh-specify`

**Created**: 2026-05-14

**Status**: Complete (Retrospec)

**Input**: User description: "First feature to include is the ability to use a generic interface to call base model with standard chat, implementing the standard provider, openai, azure openai, anthropic, cohere"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Send a Chat Message via Any Provider (Priority: P1)

As a developer, I want to send a chat completion request through a single unified interface so that I can switch LLM providers without changing my application code.

**Why this priority**: This is the core value proposition of the library. Without a unified interface, there is no product.

**Independent Test**: Can be fully tested by creating any provider client, sending a single user message, and verifying the response contains content, model name, and token usage.

**Acceptance Scenarios**:

1. **Given** an OpenAI client configured with an API key and default model, **When** I send a `ChatCompletionRequest` with a user message, **Then** I receive a `ChatCompletionResponse` with non-empty `Content`, a populated `Model`, and `PromptTokens` / `CompletionTokens` > 0.
2. **Given** an Anthropic client configured with an API key, **When** I send the same request shape, **Then** I receive the same response shape with identical properties populated.
3. **Given** an Azure OpenAI client configured with endpoint, deployment, and API key, **When** I send the same request shape, **Then** I receive the same response shape.
4. **Given** an Azure AI Inference client configured with endpoint and model ID, **When** I send the same request shape, **Then** I receive the same response shape.
5. **Given** a Cohere client configured with an API key, **When** I send the same request shape, **Then** I receive the same response shape.

---

### User Story 2 - Configure Provider via Dependency Injection (Priority: P1)

As a developer, I want to register any provider through standard .NET DI so that I can resolve `IChatCompletionClient` from the service container and benefit from HttpClient management, resilience, and lifetime handling.

**Why this priority**: DI integration is the primary consumption pattern for .NET libraries. Without it, the library is impractical for production use.

**Independent Test**: Register a provider via `services.AddOpenAiClient(...)`, resolve `IChatCompletionClient` from the service provider, and verify the resolved instance is the correct provider type.

**Acceptance Scenarios**:

1. **Given** a service collection with `AddOpenAiClient` called, **When** I resolve `IChatCompletionClient`, **Then** I get an `OpenAiChatCompletionClient` instance with resilience handlers configured.
2. **Given** a service collection with two providers registered via keyed DI (`AddOpenAiClient("openai", ...)` and `AddAnthropicClient("anthropic", ...)`), **When** I resolve by key, **Then** I get the correct provider for each key.
3. **Given** DI registration for any provider, **When** the HttpClient is created, **Then** it includes the provider's authentication handler and Polly resilience policies (retry with exponential backoff, circuit breaker).

---

### User Story 3 - Inspect Raw Request/Response for Debugging (Priority: P2)

As a developer debugging an integration issue, I want to inspect the exact JSON sent to and received from the provider API so that I can diagnose problems without external tooling.

**Why this priority**: Debuggability is a core design principle but is secondary to basic functionality.

**Independent Test**: Send a request with `IncludeRawResponse: true` and verify `RawRequestJson` and `RawResponseJson` are non-null valid JSON strings.

**Acceptance Scenarios**:

1. **Given** a request with `IncludeRawResponse = true`, **When** the API call succeeds, **Then** `RawResponseJson` contains the provider's raw JSON response and `RawRequestJson` contains the serialized request body.
2. **Given** a request with `IncludeRawResponse = false` (default), **When** the API call succeeds, **Then** `RawResponseJson` and `RawRequestJson` are both null (no memory overhead).

---

### User Story 4 - Extend Requests with ExtraParameters (Priority: P2)

As a developer, I want to inject arbitrary JSON properties into the API request body so that I can use provider-specific features not yet modeled in the unified interface.

**Why this priority**: The "escape hatch" pattern is critical for adoption — users won't adopt a library that can't keep up with rapidly evolving provider APIs.

**Independent Test**: Send a request with `ExtraParameters` containing a custom JSON property, capture the raw request, and verify the property appears in the serialized body merged with the standard fields.

**Acceptance Scenarios**:

1. **Given** a request with `ExtraParameters = {"top_p": 0.9}`, **When** the request is serialized, **Then** the outbound JSON contains `"top_p": 0.9` alongside the standard fields.
2. **Given** `ExtraParameters` with a nested object that overlaps an existing field, **When** merged, **Then** deep merge is applied (nested properties merged, not replaced).

---

### User Story 5 - Handle API Errors Without Exceptions (Priority: P2)

As a developer, I want API errors (4xx, 5xx) returned as a response object with `IsSuccess=false` rather than thrown exceptions so that I can handle failures through normal control flow.

**Why this priority**: This is a non-negotiable design principle but secondary to basic happy-path functionality.

**Independent Test**: Configure a client to hit an invalid endpoint or use invalid credentials, send a request, and verify `IsSuccess == false` with a populated `ErrorMessage`.

**Acceptance Scenarios**:

1. **Given** a provider returns HTTP 400/401/429/500, **When** the response is mapped, **Then** `IsSuccess` is `false`, `ErrorMessage` contains the status code and response body, and `Content` is empty.
2. **Given** a provider returns HTTP 400, **When** `IncludeRawResponse` is true, **Then** the raw error body is available in `ErrorMessage` or `RawResponseJson`.

---

### User Story 6 - Conversation with System, User, and Assistant Messages (Priority: P1)

As a developer, I want to send multi-turn conversations with system, user, and assistant messages so that I can build chat applications with context.

**Why this priority**: Multi-turn conversation is the fundamental use case for chat completion.

**Independent Test**: Send a request with a system message, a user message, and an assistant message, verify the response acknowledges the conversation context.

**Acceptance Scenarios**:

1. **Given** a request with `[System("You are helpful"), User("Hello"), Assistant("Hi!"), User("What did I say?")]`, **When** sent to any provider, **Then** the response references the prior conversation.
2. **Given** a request where the system message sets behavior constraints, **When** sent to Anthropic, **Then** the system message is extracted into the top-level `system` field (Anthropic's API format).

---

### User Story 7 - Configure Provider at Runtime Without DI (Priority: P3)

As a developer building a multi-tenant application, I want to create provider clients at runtime with per-tenant configuration so that I can serve multiple tenants with different API keys or endpoints.

**Why this priority**: Runtime/dynamic configuration is an advanced use case important for SaaS applications but not required for basic adoption.

**Independent Test**: Use the static `Create` factory method with explicit options, send a request, and verify it succeeds.

**Acceptance Scenarios**:

1. **Given** I call `OpenAiChatCompletionClient.Create(handlerFactory, options)` with runtime options, **When** I send a request, **Then** it uses the provided configuration (API key, base URL, model).
2. **Given** I call `AzureOpenAiChatCompletionClient.Create(handlerFactory, options, credential)` with a `TokenCredential`, **When** I send a request, **Then** Azure AD authentication is used instead of API key.

---

### User Story 8 - Discover Optional Capabilities via Feature Collection (Priority: P1)

As a developer, I want to query a client for optional capabilities at runtime so that I can write provider-agnostic code that gracefully handles unsupported features.

**Why this priority**: The feature collection pattern is the architectural foundation that enables the unified interface to be extensible without breaking changes.

**Independent Test**: Resolve a client, call `client.Features.Get<IStreamingChatFeature>()` and verify it returns non-null for providers that support it.

**Acceptance Scenarios**:

1. **Given** an OpenAI client, **When** I call `Features.Get<IJsonOutputFeature>()`, **Then** I get a non-null reference (OpenAI supports JSON output).
2. **Given** a hypothetical minimal client that only implements `IChatCompletionClient`, **When** I call `Features.Get<IStreamingChatFeature>()`, **Then** I get null (not supported).
3. **Given** any provider client, **When** I enumerate `Features`, **Then** I get all registered feature types as key-value pairs.

---

### Edge Cases

- What happens when `Model` is null and `DefaultModel` is also null? → `InvalidOperationException` is thrown (configuration error, not API error).
- What happens when the provider returns an empty response body? → `InvalidOperationException` is thrown from `LlmHttpClient`.
- What happens when Anthropic `max_tokens` is not specified? → Defaults to 8192 via `AnthropicChatCompletionClient.DefaultMaxTokens`.
- What happens when Azure OpenAI deployment is opaque (doesn't contain model name)? → `ModelName` option or routing fallback handles model-type detection.
- What happens when network is unreachable? → `HttpRequestException` propagates (network errors ARE exceptions per design principle).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a single `IChatCompletionClient` interface with a `GetChatCompletionAsync` method that accepts a `ChatCompletionRequest` and returns a `ChatCompletionResponse`.
- **FR-002**: System MUST implement `IChatCompletionClient` for OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, and Cohere providers.
- **FR-003**: System MUST support conversation messages with four roles: System, User, Assistant, and Tool.
- **FR-004**: System MUST allow model selection per-request or via a default model configured in provider options.
- **FR-005**: System MUST support `Temperature` and `MaxTokens` parameters on requests.
- **FR-006**: System MUST return token usage (`PromptTokens`, `CompletionTokens`) in every successful response.
- **FR-007**: System MUST return `IsSuccess=false` with `ErrorMessage` for API errors (4xx, 5xx) without throwing exceptions.
- **FR-008**: System MUST expose `RawResponseJson` and `RawRequestJson` when `IncludeRawResponse` is true.
- **FR-009**: System MUST support deep-merging arbitrary `ExtraParameters` JSON into outbound requests.
- **FR-010**: System MUST provide DI extension methods for each provider that register `IChatCompletionClient`, configure `HttpClient` with authentication handlers, and add Polly resilience policies.
- **FR-011**: System MUST support keyed DI registration for multi-provider scenarios.
- **FR-012**: System MUST provide static `Create` factory methods on each provider client for runtime configuration.
- **FR-013**: System MUST implement the Feature Collection pattern (`IHasFeatures`, `IFeatureCollection`) for optional capability discovery.
- **FR-014**: System MUST map provider-specific message formats (e.g., Anthropic's separate system field, Cohere's snake_case) transparently.
- **FR-015**: System MUST support multimodal messages (text + images) via `ContentParts` on `LlmMessage`.
- **FR-016**: System MUST detect model types (legacy, reasoning, GPT-5) and route to the appropriate API endpoint automatically.
- **FR-017**: System MUST emit OpenTelemetry-compatible `Activity` spans for HTTP requests when a listener is registered.
- **FR-018**: System MUST support structured logging via `ILogger` with defined event IDs for request start, completion, and failure.
- **FR-019**: System MUST provide a `FakeChatCompletionClient` in the testing package with queue-based response configuration and request capture.
- **FR-020**: System MUST handle the `Refusal` field when a model refuses to generate output (safety filtering).

### Key Entities

- **ChatCompletionRequest**: The input to any chat completion call — messages, model, temperature, max tokens, extra parameters, raw response flag, reasoning effort.
- **ChatCompletionResponse**: The output — content, model, token counts, raw JSON, success/error status, refusal, incomplete reason.
- **LlmMessage**: A single message in a conversation — role, content, optional tool call metadata, optional multimodal content parts.
- **LlmRole**: Enumeration of message roles — System, User, Assistant, Tool.
- **IFeatureCollection**: Type-safe dictionary of optional capabilities, keyed by interface type.
- **LlmHttpClient**: Shared HTTP transport handling serialization, deep merge, error mapping, streaming SSE, and telemetry.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All five providers (OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere) pass identical unit test suites verifying request mapping, response mapping, and error handling.
- **SC-002**: Switching from one provider to another requires only changing the DI registration call — zero application code changes.
- **SC-003**: `ExtraParameters` deep-merge correctly injects and nests arbitrary JSON verified by unit tests.
- **SC-004**: `RawResponseJson` / `RawRequestJson` round-trip verification: raw JSON is valid and deserializes back to the expected structure.
- **SC-005**: Integration tests against real APIs pass for all five providers (chat completion happy path).
- **SC-006**: `FakeChatCompletionClient` enables downstream consumers to write tests with zero network calls.
- **SC-007**: Polly resilience policies (3 retries, exponential backoff, circuit breaker) are configured on all DI-registered HttpClients.

## Assumptions

- Target platform is server-side .NET applications (.NET 8.0 and .NET 10).
- Consumers use `Microsoft.Extensions.DependencyInjection` as their DI container.
- API keys and endpoints are provided by the consumer (library does not manage secrets).
- Each provider's chat API is HTTP-based and returns JSON.
- Network-level failures (DNS, TCP, TLS) propagate as exceptions; only API-level errors are captured in the response object.
- Anthropic requires `max_tokens` on every request — the library defaults to 8192 when not specified.
- Azure OpenAI uses deployment-based routing; model-type detection uses deployment name, model name option, or request model as hints.

## Retrospec Metadata

**Generated**: 2026-05-14
**Source**: Reverse-engineered from existing implementation
**Analyzed files**: 50+ files across 8 projects
**Reference implementation branch**: feature/gh-specify
