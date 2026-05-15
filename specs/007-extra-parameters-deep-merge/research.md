# Research: ExtraParameters & Deep Merge

## Decision 1: Deep merge (not shallow merge or replacement)

**Decision**: `ExtraParameters` uses recursive deep merge for JSON objects. Nested objects have their properties merged; all other types (arrays, scalars, null) are replaced.

**Rationale**: Provider APIs increasingly use nested structures (`reasoning.effort`, `reasoning.summary`, `text.verbosity`, `citation_options.mode`). A shallow merge would force users to replicate all existing nested fields whenever they want to add one property inside a nested object. Deep merge preserves the library's typed nested output while letting users inject additional nested fields.

**Alternatives considered**:
- **Shallow merge (top-level only)**: Simpler but would destroy library-generated nested objects when the user provides the same top-level key. Rejected.
- **Full replacement of request body**: Maximum control but completely bypasses the library's request construction. Rejected — defeats the purpose of a unified abstraction.
- **Element-wise array merge**: Would need a merge strategy (append, replace by index, deduplicate). Too complex and provider-specific. Rejected — arrays are replaced wholesale.

## Decision 2: Merge at the LlmHttpClient layer (not per-provider)

**Decision**: The `SerializeAndMerge` method lives in `LlmHttpClient`, called by all providers via the shared `PostAsync`/`PostWithRawAsync`/`PostStreamAsync` methods.

**Rationale**: Centralizing the merge at the HTTP layer means every provider gets ExtraParameters support automatically — new providers don't need to implement merge logic. It also ensures the merge happens after all provider-specific request building (model detection, parameter mapping, etc.), so ExtraParameters truly overrides the final payload.

**Alternatives considered**:
- **Merge in each provider client**: Would require 5+ implementations of the same logic. Rejected for DRY.
- **Merge before provider-specific building**: Would merge into the unified request and then re-serialize. The provider-specific builder might not preserve unknown properties. Rejected.

## Decision 3: `JsonElement?` type (not `Dictionary<string, object>` or `JObject`)

**Decision**: `ExtraParameters` is typed as `System.Text.Json.JsonElement?`.

**Rationale**: `JsonElement` is the native `System.Text.Json` representation of arbitrary JSON. It avoids boxing, is allocation-efficient for the null case, and integrates naturally with `JsonDocument.Parse(...)`. Since the project exclusively uses `System.Text.Json` (no Newtonsoft), `JsonElement` is the idiomatic choice.

**Alternatives considered**:
- **`Dictionary<string, object>`**: Loses nested structure fidelity — `object` values need casting. Rejected.
- **`string` (raw JSON)**: Would require parsing on every request even when the caller already has a `JsonElement`. Rejected.
- **`JsonNode`**: Mutable, more overhead. The merge utility needs read-only traversal. Rejected.

## Decision 4: IncludeRawResponse as opt-in flag (not always-on)

**Decision**: Raw JSON capture requires `IncludeRawResponse: true` on the request. When false (default), both `RawRequestJson` and `RawResponseJson` are null.

**Rationale**: Capturing raw JSON requires the `PostWithRawAsync` code path which allocates additional strings. In production, most callers don't need wire-level payloads, so the overhead should be opt-in. The flag is on the request (not a client option) so callers can enable it per-request for debugging without affecting all calls.

**Alternatives considered**:
- **Always capture**: Simpler API but wastes memory in production. Rejected.
- **Client-level option**: Would capture for all requests. Too coarse for debugging one call. Rejected.
- **Separate `GetChatCompletionWithRawAsync` method**: Would double the API surface. Rejected.

## Decision 5: ArgumentException for non-object inputs (not silent no-op)

**Decision**: `JsonDeepMerge.Merge` throws `ArgumentException` if either the base or the override is not a JSON object.

**Rationale**: Passing an array or scalar as `ExtraParameters` is a programming error — the caller constructed an invalid override. Throwing immediately surfaces the bug at the call site. A silent no-op would mask the error and leave the developer confused about why their parameters aren't appearing.

**Alternatives considered**:
- **Silent no-op**: Would hide bugs. Rejected.
- **Wrap in an object**: e.g., `{"extraParameters": [1,2,3]}`. Semantically incorrect — the caller's intent was to merge, not wrap. Rejected.

## Decision 6: Property ordering — base first, then override additions

**Decision**: The merged output preserves base properties in their original order, followed by override-only properties.

**Rationale**: Property ordering in JSON is technically insignificant, but deterministic ordering aids debugging. Base-first means the standard fields (model, messages, temperature) appear before injected extras, making the merged payload easy to read.

**Alternatives considered**:
- **Alphabetical sort**: Would shuffle the payload, making it harder to compare with the unmergd version. Rejected.
- **Override-first**: Would put extras before standard fields, which is confusing when reading logs. Rejected.
