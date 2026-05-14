# Feature Specification: ExtraParameters & Deep Merge

**Feature Branch**: `007-extra-parameters-deep-merge`

**Created**: 2026-05-14

**Status**: Complete (retrospec)

**Input**: Reverse-engineered from `JsonDeepMerge`, `ExtraParameters` on request DTOs, and `IncludeRawResponse`/`RawResponseJson`/`RawRequestJson` on response DTOs

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Inject Arbitrary Parameters into Requests (Priority: P1)

As a developer using Cisharpai, I want to pass arbitrary JSON parameters that get merged into the outbound API request so that I can use bleeding-edge provider features without waiting for the library to add first-class support.

**Why this priority**: This is the core "escape hatch" — without it, users are blocked whenever a provider ships a new parameter the library doesn't model yet.

**Independent Test**: Set `ExtraParameters` on a `ChatCompletionRequest` with `{"top_p": 0.9}`, send the request, capture the HTTP body, verify `top_p` appears alongside the standard fields.

**Acceptance Scenarios**:

1. **Given** a `ChatCompletionRequest` with `ExtraParameters` containing new top-level properties, **When** the request is sent, **Then** the extra properties appear in the HTTP body alongside typed properties.
2. **Given** `ExtraParameters` that override an existing typed property (e.g., `max_tokens`), **When** the request is sent, **Then** the override value wins.
3. **Given** `ExtraParameters` is null, **When** the request is sent, **Then** no merge occurs and the body contains only the typed properties.

---

### User Story 2 - Deep Merge Nested JSON Objects (Priority: P1)

As a developer, I want nested JSON objects in `ExtraParameters` to be recursively merged with the base request so that I can add or override individual nested fields without replacing the entire object.

**Why this priority**: Provider APIs increasingly use nested structures (e.g., `reasoning.summary`, `text.verbosity`). Shallow merge would force users to replicate all existing nested fields.

**Independent Test**: Set `ExtraParameters` to `{"reasoning": {"summary": "auto"}}` on a request that already has `reasoning.effort = "medium"`, verify the merged body contains both `effort` and `summary`.

**Acceptance Scenarios**:

1. **Given** a base JSON with `{"reasoning": {"effort": "high"}}` and override `{"reasoning": {"summary": "auto"}}`, **When** merged, **Then** the result contains both `effort: "high"` and `summary: "auto"`.
2. **Given** a deeply nested base (3+ levels), **When** overrides add properties at each level, **Then** all levels are merged correctly.
3. **Given** an override that replaces an object with a scalar, **When** merged, **Then** the scalar replaces the entire object.
4. **Given** an override that replaces an array, **When** merged, **Then** the entire array is replaced (arrays are not element-merged).

---

### User Story 3 - Inspect Raw Wire-Level Payloads (Priority: P1)

As a developer debugging an LLM integration, I want to see the exact JSON sent to and received from the provider API so that I can diagnose issues without external HTTP sniffing tools.

**Why this priority**: Debuggability is a core project principle. Without raw payload access, diagnosing provider-specific issues requires setting up a proxy.

**Independent Test**: Set `IncludeRawResponse: true` on a request, send it, verify `RawRequestJson` contains the merged payload and `RawResponseJson` contains the provider's response.

**Acceptance Scenarios**:

1. **Given** `IncludeRawResponse: true` and `ExtraParameters`, **When** the response is returned, **Then** `RawRequestJson` contains the merged payload including extra parameters.
2. **Given** `IncludeRawResponse: true` without `ExtraParameters`, **When** the response is returned, **Then** both `RawRequestJson` and `RawResponseJson` are populated.
3. **Given** `IncludeRawResponse: false` (default), **When** the response is returned, **Then** `RawRequestJson` and `RawResponseJson` are null.

---

### User Story 4 - ExtraParameters Work Across All Providers (Priority: P2)

As a developer, I want `ExtraParameters` to work consistently across OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, and Cohere so that the escape hatch is universally available.

**Why this priority**: The unified abstraction principle requires that cross-cutting features work with every provider.

**Independent Test**: For each provider, set `ExtraParameters`, capture the HTTP body, verify the extra parameters appear in the request.

**Acceptance Scenarios**:

1. **Given** OpenAI with `ExtraParameters: {"top_p": 0.9}`, **When** sent, **Then** the body contains `top_p`.
2. **Given** Anthropic with `ExtraParameters: {"top_k": 40}`, **When** sent, **Then** the body contains `top_k`.
3. **Given** Azure OpenAI with `ExtraParameters: {"presence_penalty": 0.5}`, **When** sent, **Then** the body contains `presence_penalty`.
4. **Given** Azure AI Inference with `ExtraParameters: {"top_p": 0.9}`, **When** sent, **Then** the body contains `top_p`.

---

### User Story 5 - ExtraParameters on Embedding Requests (Priority: P2)

As a developer using embedding features, I want `ExtraParameters` available on `EmbeddingRequest` too so that the escape hatch covers all API surfaces.

**Why this priority**: Embedding APIs also evolve; the same extensibility principle applies.

**Independent Test**: Set `ExtraParameters` on an `EmbeddingRequest`, send it, verify the extra parameters appear in the HTTP body.

**Acceptance Scenarios**:

1. **Given** an `EmbeddingRequest` with `ExtraParameters`, **When** sent, **Then** the extra parameters are merged into the request body.
2. **Given** an `EmbeddingRequest` with `IncludeRawResponse: true`, **When** sent, **Then** `RawRequestJson` and `RawResponseJson` are populated.

---

### Edge Cases

- What happens when `ExtraParameters` is not a JSON object (e.g., an array)? → `ArgumentException` is thrown by `JsonDeepMerge.Merge`.
- What happens when the base JSON is not an object? → `ArgumentException` is thrown.
- What happens when `ExtraParameters` is an empty object `{}`? → No-op; the original body is returned unchanged.
- What happens when an override sets a value to `null`? → The property is set to JSON `null` in the merged output.
- What happens when an override replaces a scalar with an object? → The object replaces the scalar.
- What happens when base and override have the same key with different types? → The override value wins regardless of type.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept an optional `ExtraParameters` JSON object on chat completion requests
- **FR-002**: System MUST accept an optional `ExtraParameters` JSON object on embedding requests
- **FR-003**: System MUST deep-merge `ExtraParameters` into the serialized request body before sending
- **FR-004**: Deep merge MUST recursively merge nested JSON objects (properties from both sides preserved)
- **FR-005**: Deep merge MUST replace (not merge) arrays, scalars, booleans, and nulls
- **FR-006**: Deep merge MUST allow overrides to replace an existing property's value regardless of type
- **FR-007**: Deep merge MUST preserve base properties not present in the override
- **FR-008**: Deep merge MUST add override properties not present in the base
- **FR-009**: System MUST validate that both base and override are JSON objects, throwing `ArgumentException` otherwise
- **FR-010**: System MUST skip merge when `ExtraParameters` is null (no performance overhead)
- **FR-011**: System MUST support `IncludeRawResponse` flag on both chat and embedding requests
- **FR-012**: When `IncludeRawResponse` is true, response MUST include `RawRequestJson` (the merged payload) and `RawResponseJson` (the provider response)
- **FR-013**: When `IncludeRawResponse` is false (default), `RawRequestJson` and `RawResponseJson` MUST be null
- **FR-014**: ExtraParameters MUST work with all 5 chat providers (OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere)
- **FR-015**: ExtraParameters deep-merge MUST apply to streaming, tool calling, JSON output, and grounded chat request paths

### Key Entities

- **JsonDeepMerge**: Static utility for recursively merging JSON documents
- **ExtraParameters**: Optional `JsonElement?` property on request DTOs
- **IncludeRawResponse**: Boolean flag controlling raw JSON capture
- **RawRequestJson / RawResponseJson**: String properties on response DTOs for wire-level inspection

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 19 unit tests pass for `JsonDeepMerge` covering all merge semantics
- **SC-002**: ExtraParameters merge verified for all 5 providers via HTTP body capture tests
- **SC-003**: `IncludeRawResponse` returns both raw JSON strings for all providers
- **SC-004**: Null `ExtraParameters` causes zero overhead (no parse, no merge)
- **SC-005**: Deep merge correctly handles 3+ levels of nesting

## Assumptions

- `ExtraParameters` is a `System.Text.Json.JsonElement?` — callers construct it via `JsonDocument.Parse(...).RootElement`
- The merge happens at the `LlmHttpClient` layer, making it provider-agnostic
- Arrays in `ExtraParameters` replace base arrays entirely (element-level merge is not supported)
- `RawRequestJson` reflects the post-merge payload, not the pre-merge typed model
- The feature has no runtime dependency beyond `System.Text.Json` (part of the .NET runtime)

## Retrospec Metadata

**Generated**: 2026-05-14
**Source**: Reverse-engineered from existing implementation
**Analyzed files**: 12 files across 6 projects
**Reference implementation branch**: feature/gh-specify
