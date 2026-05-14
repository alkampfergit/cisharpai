# Research: Grounded Chat

## Decision 1: Default citation mode is `Fast` (not `Accurate`)

**Decision**: `GroundedChatOptions.CitationMode` defaults to `CitationMode.Fast`.

**Rationale**: `Fast` is supported by all Cohere model families (both `command-r` and `command-a`). `Accurate` is rejected by `command-a` models with HTTP 400, making it unsafe as a default. Choosing `Fast` as the cross-model default prevents silent failures when users switch models.

**Alternatives considered**:
- **Default to `Accurate`**: More precise but breaks on `command-a` models, which are the newer family. Rejected because a default should work everywhere.
- **Default to `Enabled`**: Delegates to the provider, but the behavior is less predictable. Rejected for clarity.

## Decision 2: Silent downgrade of `Accurate` to `Fast` on `command-a` models

**Decision**: When `CitationMode.Accurate` is requested for a `command-a*` model, the client logs a warning and sends `FAST` instead of forwarding the unsupported value.

**Rationale**: Letting the API return HTTP 400 for a valid-looking request creates a confusing developer experience. The downgrade is transparent (via logging) and preserves functionality. This matches the general Cisharpai principle of graceful degradation.

**Alternatives considered**:
- **Throw `ArgumentException`**: Would break the no-exceptions principle for API-level concerns.
- **Return error response**: The request would still work with `Fast`, so failing it outright wastes a valid opportunity.
- **Forward and let the API reject**: Confusing error message from Cohere about an invalid citation mode.

## Decision 3: `DocumentChunk` uses mutually exclusive `Data` and `Text` fields

**Decision**: A single `DocumentChunk` record has nullable `Data` (dictionary) and `Text` (string) fields, with runtime validation enforcing exactly one is set.

**Rationale**: Cohere's API serializes structured documents as JSON objects and plain-text documents as JSON strings in the `data` field. Using two fields on a single type (rather than a type hierarchy) keeps the API surface minimal and avoids requiring pattern matching for simple document creation.

**Alternatives considered**:
- **Abstract base class with two subtypes**: Cleaner type safety but more ceremony for a two-variant case. Rejected for simplicity.
- **Single `object Data` field**: Loses type safety entirely. Rejected.

## Decision 4: `GroundedChatCompletionResponse` wraps `ChatCompletionResponse` (not inherits)

**Decision**: The grounded response contains a `ChatCompletion` property plus `Citations`, with convenience delegation properties for `IsSuccess`, `Content`, and `ErrorMessage`.

**Rationale**: C# records don't support inheritance well for immutable DTOs. Composition keeps both types simple and allows the grounded response to reuse the standard error factory (`ChatCompletionResponse.Error()`). The convenience properties avoid forcing callers to navigate `.ChatCompletion.` for common fields.

**Alternatives considered**:
- **Inherit from `ChatCompletionResponse`**: Records with inheritance are awkward; the `with` expression doesn't compose well across levels.
- **Flat response with all fields**: Would duplicate 10+ fields from `ChatCompletionResponse`.

## Decision 5: Grounded chat does not set `response_format`

**Decision**: The `CohereChatCompletionClient` explicitly omits `response_format` when building grounded chat requests.

**Rationale**: Cohere's API rejects requests that include both `documents` and `response_format`. The test `GroundedChat_DoesNotSetResponseFormat` explicitly verifies this. Grounded chat and JSON Mode are mutually exclusive features at the provider level.

**Alternatives considered**:
- **Allow both and let the API reject**: Would produce a confusing error. Rejected in favor of preventing the invalid combination.
