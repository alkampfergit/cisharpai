# Research: Tool Calling

Technical decisions observed in the implementation, with inferred rationale and rejected alternatives.

## Decision 1: Shared ToolCallingHelper with Generic Mappers

**Decision**: Tool call parsing, choice mapping, and streaming delta mapping are centralised in a static `ToolCallingHelper` class using generic methods (`MapResponseToolCalls<T>`, `MapToolChoice`, `MapStreamToolCallDelta<T>`).

**Rationale**: All five providers share the same three-step pattern: (1) extract (Id, FunctionName, Arguments) from provider-specific types, (2) parse JSON arguments, (3) wrap in unified `ToolCall`. The generic approach lets each provider supply an extractor lambda while the helper handles JSON parsing and error recovery. This eliminates ~100 lines of duplicated argument-parsing logic across five providers.

**Alternatives rejected**:
- **Per-provider parsing**: Would duplicate the argument parse/fallback logic five times with high risk of inconsistency.
- **Base class with template method**: Would require providers to share a common base, which conflicts with the existing design where each provider is an independent sealed class.
- **Interface with default methods**: C# default interface methods would work but add unnecessary coupling; a static helper is simpler.

## Decision 2: Graceful Argument Parsing (Wrap as Raw String)

**Decision**: When `JsonDocument.Parse(arguments)` fails, the arguments string is wrapped as a JSON string value (`"\"unparseable text\""`) rather than throwing an exception.

**Rationale**: Some models (especially smaller/older ones) occasionally return malformed JSON in tool call arguments. Throwing would break the entire response pipeline for a single bad argument. Wrapping as a string preserves the raw content for debugging while letting the caller decide how to handle it.

**Alternatives rejected**:
- **Throw exception**: Violates the library's "no exceptions for API errors" principle (Constitution II).
- **Return null arguments**: Loses the original content, making debugging harder.
- **Return a special error ToolCall**: Adds complexity to the ToolCall type for an edge case.

## Decision 3: Abstract Record with Private Constructor for ToolChoice

**Decision**: `ToolChoice` is an abstract record with a private constructor and four internal sealed subtypes (`AutoChoice`, `NoneChoice`, `RequiredChoice`, `SpecificChoice`). Public access is via static fields/methods (`ToolChoice.Auto`, `ToolChoice.Specific("name")`).

**Rationale**: This pattern creates a closed set of choices that cannot be extended by consumers, enabling exhaustive pattern matching in provider mapping code. The static fields provide a clean API surface while the private constructor prevents arbitrary subclassing.

**Alternatives rejected**:
- **Enum**: Cannot carry the function name for `Specific`.
- **Open class hierarchy**: Would allow consumers to create invalid tool choices.
- **Tuple/string**: Loses type safety and intent.

## Decision 4: Cohere Specific-to-Required Degradation

**Decision**: When Cohere receives `ToolChoice.Specific("name")`, it silently degrades to `"REQUIRED"` rather than throwing or ignoring the choice.

**Rationale**: Cohere's v2 API does not support forcing a specific tool. Degrading to `REQUIRED` (model must call some tool) is the closest available behavior and avoids runtime failures for cross-provider code. The degradation is documented in the wiki provider differences table.

**Alternatives rejected**:
- **Throw NotSupportedException**: Would force callers to add provider-specific branching, defeating the unified abstraction.
- **Ignore and use AUTO**: Less predictable; the caller explicitly asked for tool enforcement.
- **Send the function name anyway**: Would cause an API error from Cohere.

## Decision 5: Anthropic Tool Result as User Role

**Decision**: When sending tool results to Anthropic, they are formatted as `user` role messages with `tool_result` content blocks, not as a separate `tool` role.

**Rationale**: This is dictated by Anthropic's API design — Anthropic does not have a `tool` role. The provider implementation maps `LlmMessage` with `LlmRole.Tool` to Anthropic's expected format transparently, so callers use the same unified message format regardless of provider.

**Alternatives rejected**:
- **Require callers to format Anthropic-style messages**: Would leak provider details through the abstraction (violates Constitution I).
- **Use a separate method for Anthropic tool results**: Same issue; the interface should be identical across providers.

## Decision 6: ToolCallingResponse Wraps Rather Than Extends ChatCompletionResponse

**Decision**: `ToolCallingResponse` contains a `ChatCompletion` field of type `ChatCompletionResponse` rather than inheriting from it. Convenience properties (`IsSuccess`, `ErrorMessage`, `Content`) delegate to the inner response.

**Rationale**: C# records support inheritance but it creates complications with equality, `with` expressions, and serialization. Composition via wrapping is cleaner and avoids the "fragile base record" problem. The convenience delegates ensure callers don't need to reach through `.ChatCompletion` for common checks.

**Alternatives rejected**:
- **Inherit from ChatCompletionResponse**: Record inheritance is fragile with positional parameters and `with` semantics.
- **Return ChatCompletionResponse with ToolCalls added**: Would require mutating the base type or adding optional fields that most callers don't need.

## Decision 7: Validation Before HTTP Call

**Decision**: `ToolCallingOptions.Validate()` and `ToolDefinition.Validate()` are called before making any HTTP request. Invalid input throws `ArgumentException`.

**Rationale**: Catching invalid tool definitions early (empty names, non-object parameters, empty tool lists) gives callers clear, immediate error messages rather than cryptic provider API errors. This is one of the two exception categories allowed by the constitution (configuration errors).

**Alternatives rejected**:
- **Let the provider API reject it**: Error messages would be inconsistent across providers and harder to diagnose.
- **Return an error response**: Would conflate input validation with API errors; `ArgumentException` is the established pattern for configuration mistakes (per Constitution II — exceptions for config errors are acceptable).
