# Feature Specification: DI Client Factory

**Feature Branch**: `009-di-client-factory`

**Created**: 2026-05-15

**Status**: Draft

**Input**: User description: "Better support for creating concrete class by configuration — a factory interface in the core library that accepts a runtime configuration and requested interface type, and resolves the correct implementation through standard DI."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register Provider Support at Startup (Priority: P1)

A library consumer configures their DI container at startup, registering the factory and declaring which providers they want available at runtime. They do not yet know which provider a given request will target — that is decided later by runtime configuration.

**Why this priority**: Without provider registration, no runtime resolution is possible. This is the foundation for the entire feature.

**Independent Test**: Can be fully tested by registering the factory with one or more providers in an `IServiceCollection`, building the service provider, and resolving the factory interface — delivers the ability to enumerate supported providers.

**Acceptance Scenarios**:

1. **Given** a new `ServiceCollection`, **When** the consumer calls the factory registration helper and adds OpenAI and Anthropic support, **Then** the factory is resolvable from the built `IServiceProvider` and reports both providers as available.
2. **Given** a new `ServiceCollection` with the factory registered, **When** no provider support is added, **Then** the factory is still resolvable but reports zero available providers.
3. **Given** a new `ServiceCollection`, **When** the consumer adds the same provider support twice, **Then** registration succeeds without error (idempotent).

---

### User Story 2 - Create Chat Completion Client at Runtime (Priority: P1)

A consumer holds a runtime configuration object (provider type + connection details) and asks the factory for an `IChatCompletionClient`. The factory uses DI to create the client with the standard resilience pipeline and HttpClient management — identical to what the existing `Add*Client` DI helpers produce.

**Why this priority**: This is the core value proposition — runtime client creation with full DI benefits.

**Independent Test**: Can be fully tested by registering the factory with at least one provider, then calling the factory with a runtime configuration and verifying the returned client is functional (makes a mocked HTTP call) and benefits from the resilience handler.

**Acceptance Scenarios**:

1. **Given** a factory with OpenAI support registered, **When** the consumer requests an `IChatCompletionClient` with an OpenAI runtime configuration, **Then** the factory returns a fully configured `OpenAiChatCompletionClient` that uses the DI-managed `HttpClient` with resilience handlers.
2. **Given** a factory with OpenAI and Anthropic support registered, **When** the consumer requests an `IChatCompletionClient` with an Anthropic runtime configuration, **Then** the factory returns a fully configured `AnthropicChatCompletionClient`.
3. **Given** a factory with only OpenAI support registered, **When** the consumer requests an `IChatCompletionClient` with a Cohere runtime configuration, **Then** the factory returns a clear error indicating the provider is not registered.

---

### User Story 3 - Create Embedding Client at Runtime (Priority: P2)

A consumer uses the same factory to create an `IEmbeddingClient` at runtime. Not all providers support embeddings, and the factory communicates this clearly.

**Why this priority**: Embedding support broadens the factory beyond chat, but chat is the primary use case.

**Independent Test**: Can be fully tested by registering the factory with a provider that supports embeddings (e.g., OpenAI), then requesting an `IEmbeddingClient` with a runtime configuration.

**Acceptance Scenarios**:

1. **Given** a factory with OpenAI support registered, **When** the consumer requests an `IEmbeddingClient` with an OpenAI runtime configuration, **Then** the factory returns a fully configured `OpenAiEmbeddingClient`.
2. **Given** a factory with Anthropic support registered, **When** the consumer requests an `IEmbeddingClient` with an Anthropic runtime configuration, **Then** the factory returns a clear error indicating Anthropic does not support embeddings.

---

### User Story 4 - Runtime Configuration Object (Priority: P1)

The consumer provides a unified runtime configuration that identifies the provider and supplies connection details (API key, endpoint, model). The configuration is provider-agnostic from the consumer's perspective.

**Why this priority**: The configuration shape determines the consumer-facing API — it must be clear and consistent.

**Independent Test**: Can be fully tested by constructing various runtime configurations and validating they carry the expected data and can be distinguished by provider type.

**Acceptance Scenarios**:

1. **Given** a runtime configuration specifying "OpenAI" as the provider with an API key and model name, **When** the configuration is passed to the factory, **Then** the factory correctly identifies OpenAI as the target provider and extracts the connection parameters.
2. **Given** a runtime configuration specifying "AzureOpenAi" as the provider with an endpoint and API key, **When** the configuration is passed to the factory, **Then** the factory correctly identifies Azure OpenAI as the target provider.
3. **Given** a runtime configuration with an unrecognized provider name, **When** the configuration is passed to the factory, **Then** the factory returns a clear error identifying the unknown provider.

---

### Edge Cases

- What happens when two providers are registered and the runtime configuration is ambiguous (e.g., missing provider type)? The factory returns an error — provider type is mandatory.
- What happens when the consumer calls the factory concurrently from multiple threads with different configurations? The factory is thread-safe; each call produces an independent client instance.
- What happens when the runtime configuration has an invalid API key or endpoint? The factory creates the client (it does not validate credentials); the error surfaces on the first API call, consistent with existing behaviour.
- What happens when a provider-specific option (e.g., OpenAI `BaseUrl` override) is needed? The runtime configuration supports an optional provider-specific settings section.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The core library (`Cisharpai`) MUST define a factory interface that accepts a runtime configuration and returns a client for a requested interface type (`IChatCompletionClient` or `IEmbeddingClient`).
- **FR-002**: Each provider project MUST expose a registration helper that teaches the factory how to create its clients (e.g., `AddOpenAiFactorySupport()`).
- **FR-003**: Clients created by the factory MUST use DI-managed `HttpClient` instances with the standard Cisharpai resilience handler, identical to clients registered via existing `Add*Client` methods.
- **FR-004**: The factory MUST return a clear, descriptive error (not throw) when the requested provider is not registered or does not support the requested interface type, consistent with the library's "no exceptions for API errors" pattern.
- **FR-005**: The runtime configuration MUST include at minimum: provider identifier, API key, and model name. Additional provider-specific settings (endpoint URL, API version, deployment name) MUST be supported.
- **FR-006**: The factory MUST be thread-safe — concurrent calls with different configurations produce independent client instances without interference.
- **FR-007**: The factory interface MUST live in the `Cisharpai` core project so consumers can depend on it without referencing provider-specific packages.
- **FR-008**: Provider registration helpers MUST be idempotent — calling the same registration twice does not duplicate handlers or cause errors.

### Key Entities

- **Runtime Client Configuration**: A provider-agnostic configuration object that identifies the target provider and carries connection details (API key, endpoint, model, optional provider-specific settings). Immutable record consistent with existing DTOs.
- **Client Factory**: The core interface that accepts a runtime configuration and returns the requested client type. Lives in `Cisharpai` core.
- **Provider Registration**: Each provider project contributes a descriptor that maps a provider identifier to a client-creation function, registered at startup.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A consumer can switch between any registered provider at runtime by changing only the configuration object — no code changes, no DI re-registration.
- **SC-002**: Clients created by the factory exhibit identical resilience behaviour (retry, circuit breaker, timeout) to clients registered via existing `Add*Client` helpers.
- **SC-003**: All five existing providers (OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere) have factory registration support.
- **SC-004**: Unit tests cover every provider's chat completion factory path; providers that support embeddings also have embedding factory tests.
- **SC-005**: A consumer can create and use a factory-produced client in under 5 lines of code beyond initial DI setup.

## Assumptions

- The factory does not replace the existing `Add*Client` DI registration pattern — both coexist. Consumers who know their provider at startup continue using the existing pattern.
- The factory creates a new client instance per call — it does not cache or pool clients. Consumers wanting caching manage it themselves.
- The factory does not validate credentials or connectivity at creation time — validation happens on first API call, consistent with existing client behaviour.
- The runtime configuration is a simple data object (immutable record) — it does not carry behaviour or service references.
- Provider-specific advanced options (like OpenAI's Responses API selection or Azure deployment names) are supported through the configuration's extensible settings section.
