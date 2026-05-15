# Research: DI Client Factory

## R-001: Factory Error Handling Pattern

**Decision**: Use a generic result wrapper `CisharpaiClientFactoryResult<T>` with `IsSuccess`, `Client`, and `ErrorMessage` properties, consistent with how `ChatCompletionResponse` uses `IsSuccess`/`ErrorMessage`.

**Rationale**: The spec explicitly requires "return a clear, descriptive error (not throw)" for unregistered providers and unsupported interface types. While the constitution allows exceptions for "configuration errors", the owner approved the no-throw factory spec. A result wrapper keeps the factory API consistent with the rest of the library's error philosophy.

**Alternatives considered**:
- Throwing `InvalidOperationException` — allowed by constitution for config errors, but contradicts spec FR-004
- Nullable return with out-parameter error — less ergonomic, not idiomatic C# records
- `OneOf<T, Error>` — external dependency, overkill for this use case

## R-002: Provider Identification Strategy

**Decision**: Use a `CisharpaiProvider` enum in the core project with values: `OpenAi`, `AzureOpenAi`, `AzureAiInference`, `Anthropic`, `Cohere`. The `RuntimeClientConfiguration` carries a `Provider` property of this type.

**Rationale**: An enum provides compile-time safety, IDE auto-complete, and prevents typos. The set of providers is finite and changes rarely. Each provider project registers under its enum value.

**Alternatives considered**:
- String identifiers — extensible but error-prone, no compile-time checking
- Type-based (typeof(OpenAiChatCompletionClient)) — leaks provider types into core
- Interface marker types — over-engineered for 5 known providers

## R-003: HttpClient Lifecycle in Factory-Created Clients

**Decision**: Factory-created clients use `IHttpClientFactory.CreateClient(name)` where each factory provider registers a named HttpClient with the resilience handler at DI registration time. The factory resolves the named client on each `Create` call.

**Rationale**: This mirrors exactly how the existing `Add*Client` extensions work — they register named HttpClients via `services.AddHttpClient<T>()` with resilience handlers. The factory registration helpers do the same but use a factory-specific name prefix (e.g., `CisharpaiFactory_OpenAi_Chat`) so they don't collide with existing registrations.

**Alternatives considered**:
- Creating raw HttpClient with `new HttpClient()` — loses resilience handlers (the exact problem being solved)
- Sharing existing named HttpClients — would collide with existing DI registrations and break single-registration consumers
- Using `IHttpMessageHandlerFactory` directly — loses the HttpClient lifecycle management benefits

## R-004: Provider Descriptor Pattern

**Decision**: Define an `IClientFactoryProvider` interface in core with methods `CreateChatCompletionClient(IServiceProvider, RuntimeClientConfiguration)` and `CreateEmbeddingClient(IServiceProvider, RuntimeClientConfiguration)`. Each provider implements this. The factory holds a `Dictionary<CisharpaiProvider, IClientFactoryProvider>` populated at registration.

**Rationale**: Clean separation — core defines the contract, each provider implements it. The `IServiceProvider` parameter gives access to `IHttpClientFactory`, `ILoggerFactory`, and any other DI services. Embedding methods return the result wrapper so providers that don't support embeddings can return `IsSuccess=false`.

**Alternatives considered**:
- `Func<>` delegates instead of an interface — harder to test, less discoverable
- A single `Create(Type interfaceType, ...)` with runtime type switching — loses type safety
- Registration via attributes/reflection — implicit, harder to debug

## R-005: Runtime Configuration Shape

**Decision**: `RuntimeClientConfiguration` is an immutable record with: `Provider` (enum), `ApiKey` (string), `Model` (string?), `Endpoint` (string?), `ExtraSettings` (IReadOnlyDictionary<string, string>?). Provider-specific settings (deployment name, API version, organization, etc.) go in `ExtraSettings`.

**Rationale**: A flat record with an extensible dictionary for provider-specific settings keeps the core type simple while supporting all existing options. Each provider's factory descriptor knows which keys to extract from `ExtraSettings`. The dictionary pattern matches the existing `ExtraParameters` deep-merge philosophy.

**Alternatives considered**:
- Inheritance hierarchy (OpenAiRuntimeConfig : RuntimeClientConfiguration) — defeats the purpose of a provider-agnostic config
- JSON object for extra settings — adds serialization dependency to a pure configuration object
- Separate `ProviderSettings` record per provider — requires core to reference provider types
