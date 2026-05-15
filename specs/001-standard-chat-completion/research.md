# Research: Standard Chat Completion

## Technical Decisions

### Decision 1: Raw HttpClient instead of Provider SDKs

**Decision**: Use raw `HttpClient` with custom `DelegatingHandler` for authentication instead of wrapping provider SDKs (e.g., `OpenAI` NuGet, `Azure.AI.OpenAI`).

**Rationale**: Provider SDKs add their own abstractions, making it difficult to present a truly unified interface. They also dictate dependency versions and may conflict with each other. Raw HTTP gives full control over serialization, error handling, and the wire format.

**Alternatives considered**:
- Wrap each provider's official SDK → Rejected: SDK APIs diverge significantly; would need adapter layers as complex as the raw HTTP approach, with extra dependency burden.
- Use `Refit` or similar typed HTTP client generator → Rejected: Schema changes frequently; hand-rolled serialization with `System.Text.Json` provides more control over deep-merge and provider-specific quirks.

### Decision 2: Feature Collection Pattern for Optional Capabilities

**Decision**: Use the `IFeatureCollection` (type-keyed dictionary) pattern from ASP.NET Core rather than multiple interfaces on `IChatCompletionClient` or a capabilities enum.

**Rationale**: Adding new features (streaming, tool calling, JSON output, grounded chat) shouldn't change the base interface. The feature collection allows adding capabilities without breaking changes and supports compile-time type safety.

**Alternatives considered**:
- Extend `IChatCompletionClient` with all methods → Rejected: Providers that don't support a feature would need to throw `NotSupportedException`, violating the "no exceptions for API errors" principle.
- Capabilities enum/flags → Rejected: No type safety; callers would still need to cast to access feature-specific methods.
- Marker interfaces on the client class → Rejected: Requires `is` checks and casting; less discoverable than `Features.Get<T>()`.

### Decision 3: Immutable Records for Request/Response DTOs

**Decision**: All request and response types are `sealed record` types with positional constructor syntax.

**Rationale**: Records provide value equality, immutability, and `with` expression support. Callers can derive modified requests via `request with { Model = "gpt-4o" }` without mutating the original. Thread safety is guaranteed without synchronization.

**Alternatives considered**:
- Mutable POCO classes with builders → Rejected: Thread safety concerns in async pipelines; more ceremony for construction.
- Readonly structs → Rejected: Response types carry strings and collections, making them too large for stack allocation benefits.

### Decision 4: No Exceptions for API Errors

**Decision**: `GetChatCompletionAsync` catches `LlmHttpRequestException` (thrown by `LlmHttpClient` for non-2xx responses) and returns `ChatCompletionResponse.Error(...)`. Network errors still throw.

**Rationale**: API errors (rate limiting, invalid model, context too long) are expected in normal operation. Forcing try/catch for routine error handling is verbose and error-prone. The response object pattern lets callers use simple `if (response.IsSuccess)` checks.

**Alternatives considered**:
- Always throw, like most HTTP client libraries → Rejected: Would require callers to catch and handle multiple exception types; doesn't match the "results, not exceptions" philosophy.
- Return `Result<T>` monad → Rejected: Adds a dependency or custom type; .NET ecosystem favors the response-with-status pattern (similar to `HttpResponseMessage`).

### Decision 5: ExtraParameters Deep Merge

**Decision**: `ExtraParameters` is a `JsonElement?` that is deep-merged (not shallow-merged) into the serialized request JSON via `JsonDeepMerge`.

**Rationale**: Providers rapidly add new parameters. Deep merge allows callers to extend nested objects (e.g., adding a property inside `response_format`) without replacing the entire object constructed by the library.

**Alternatives considered**:
- Dictionary of key-value pairs → Rejected: Cannot represent nested structures.
- Shallow merge → Rejected: Would replace entire nested objects, breaking library-constructed values.
- Strongly-typed extension options → Rejected: Can't keep pace with provider API changes; defeats the "escape hatch" purpose.

### Decision 6: Azure OpenAI Model-Type Routing with Fallback

**Decision**: Azure OpenAI client detects model type (legacy, reasoning, GPT-5) from hints (ModelName option, request model, deployment name) and routes to the appropriate API (Chat Completions vs Responses API). On 404, it falls back to the alternate route and caches the result.

**Rationale**: Azure OpenAI deployments may have opaque names that don't reveal the underlying model. The routing cache with fallback avoids repeated 404s while supporting transparent upgrades when Azure adds new API versions.

**Alternatives considered**:
- Require explicit API selection → Rejected: Adds configuration burden; users may not know which API their deployment supports.
- Always use Chat Completions → Rejected: GPT-5 on Azure requires the Responses API.

### Decision 7: Shared LlmHttpClient

**Decision**: All providers share a single `LlmHttpClient` class for HTTP transport, serialization, telemetry, and SSE stream parsing.

**Rationale**: Avoids duplicating HTTP, logging, and tracing logic across 5 providers. The class is generic enough (typed POST with JSON serialization) to work for all providers despite different wire formats.

**Alternatives considered**:
- Each provider manages its own HttpClient calls → Rejected: Would duplicate telemetry, error handling, and SSE parsing across 5 implementations.
- Abstract base class for providers → Rejected: Providers have different enough request structures that a shared base would be overly abstract.
