# Research: Testing Package

## Decision 1: Queue + Default pattern (not mock framework)

**Decision**: Fake clients use explicit FIFO queues and nullable default responses instead of requiring a mock framework like NSubstitute.

**Rationale**: The testing package is consumed by downstream users of the library, not just the library's own test suite. Requiring NSubstitute (or any mock framework) as a transitive dependency would constrain users' test infrastructure choices. Explicit queues are simpler to understand and debug — the test author sees exactly what will be returned without learning a mock DSL. The queue-then-default fallback covers both sequential multi-call scenarios and simple single-response tests.

**Alternatives considered**:
- **NSubstitute-based fakes**: More flexible but introduces a mandatory transitive dependency. Rejected because consumers may use different mock frameworks (Moq, FakeItEasy, etc.).
- **Func-based delegates**: e.g., `Func<ChatCompletionRequest, ChatCompletionResponse>`. More powerful but harder to use for the common case of returning a static response. Rejected for simplicity.
- **Builder pattern**: e.g., `fake.When(r => r.Messages.Count > 1).ThenReturn(...)`. Too complex for a lightweight testing library. Rejected.

## Decision 2: Sealed classes (not interfaces or abstract base)

**Decision**: `FakeChatCompletionClient` and `FakeEmbeddingClient` are `sealed` classes, not abstract bases or additional interfaces.

**Rationale**: The fakes are concrete test utilities, not extension points. Sealing them communicates that they are not meant to be subclassed and allows the JIT to devirtualize method calls. If a user needs custom behavior beyond queue/default, they should create their own `IChatCompletionClient` implementation.

**Alternatives considered**:
- **Abstract `FakeClientBase<TResponse>`**: Would reduce some code duplication between chat and embedding clients, but the two clients have different response types per feature method, making a shared base awkward. Rejected.
- **Interface `IFakeChatClient`**: Would add ceremony without value — users interact with the concrete type in tests. Rejected.

## Decision 3: Feature interfaces implemented directly (self-registration)

**Decision**: `FakeChatCompletionClient` implements all four feature interfaces itself (rather than having separate fake feature objects) and registers `this` into the `FeatureCollection`.

**Rationale**: This keeps the testing API simple — one object to create, configure, and assert against. Having separate `FakeStreamingFeature`, `FakeToolCallingFeature`, etc. would require wiring them together and splitting queue/capture state across objects.

**Alternatives considered**:
- **Separate fake feature objects**: More realistic (mirrors real providers where the client class itself implements features), but the separation adds complexity without testing value. Rejected.
- **Decorator pattern**: Wrap a base fake with feature decorators. Too much ceremony for test utilities. Rejected.

## Decision 4: Flags enum for feature opt-out (not boolean parameters)

**Decision**: `FakeChatFeatures` and `FakeEmbeddingFeatures` are `[Flags]` enums with `None` and `All` values.

**Rationale**: Flags enums are composable (`Streaming | ToolCalling`), self-documenting, and avoid parameter explosion. The default is `All` so that most tests get all features without configuration. Only tests that specifically verify feature-detection code paths need to pass a selective value.

**Alternatives considered**:
- **Boolean parameters per feature**: `new FakeChatCompletionClient(streaming: true, toolCalling: false, ...)`. Verbose and scales poorly as features are added. Rejected.
- **Builder pattern**: `FakeChatCompletionClient.Create().WithStreaming().WithToolCalling().Build()`. Too much ceremony. Rejected.

## Decision 5: Static factory class (not instance builders)

**Decision**: `FakeResponses` is a static class with factory methods, not an instance-based builder.

**Rationale**: Response creation is stateless — each factory method produces one response object from parameters. There is no state to accumulate, so an instance-based builder would add unnecessary ceremony. Static methods are the simplest way to express this.

**Alternatives considered**:
- **Instance builder**: `new FakeResponseBuilder().WithContent("hi").WithModel("gpt-4").Build()`. More flexible but overkill when named parameters already provide the same clarity. Rejected.
- **Extension methods on response types**: e.g., `ChatCompletionResponse.Fake("content")`. Would pollute the production types with test-only methods. Rejected.

## Decision 6: DI extensions return the fake instance

**Decision**: `AddFakeChatCompletionClient()` returns the `FakeChatCompletionClient` instance, not `IServiceCollection`.

**Rationale**: The returned instance is needed immediately for configuring default responses and later for assertions. Returning `IServiceCollection` (the standard pattern) would force the test to resolve the fake from the container or keep a separate reference, adding boilerplate.

**Alternatives considered**:
- **Return `IServiceCollection`**: Standard fluent pattern but inconvenient — the test needs the concrete fake type for `EnqueueResponse`/`ReceivedRequests`. Rejected.
- **`out` parameter**: `services.AddFakeChatCompletionClient(out var fake)`. C# supports it but it's less clean than a return value. Rejected.

## Decision 7: `InvalidOperationException` when no response configured

**Decision**: When both the queue and default are empty/null, the fake throws `InvalidOperationException` with a descriptive message.

**Rationale**: Silently returning null or a default empty response would mask test configuration errors. Throwing immediately when the fake is unconfigured ensures the test author knows they forgot to set up responses. This is an intentional deviation from the "no exceptions for API errors" principle — the fake is a test utility, not an API client, so the exception signals a test setup error, not an API failure.

**Alternatives considered**:
- **Return `IsSuccess=false` with error message**: Would match the production pattern but would be confusing — the "error" isn't from an API, it's from the test setup. Rejected.
- **Return null**: Would cause NullReferenceException downstream, which is harder to diagnose. Rejected.
