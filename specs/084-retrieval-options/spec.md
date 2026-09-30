# Feature Specification: RetrievalOptions with Portable Filters and Provider Query Extensions

**Feature Branch**: `089-retrieval-options`

**Created**: 2026-09-23

**Status**: Draft

**Input**: User description: "Change the shipped IRetriever call contract to accept one RetrievalOptions object containing a deliberately small common query surface and an optional provider-specific query extension."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Basic Retrieval with Common Options (Priority: P1)

A library consumer retrieves relevant chunks from any retriever implementation using only the portable common options — top-K limit, minimum score threshold, and AND-equality metadata filters — without knowing or caring which retrieval backend is behind the interface.

**Why this priority**: This is the core value proposition. Every retriever must honour the common options, and every caller must be able to use them portably across backends.

**Independent Test**: Can be fully tested with the in-memory retriever by ingesting chunks with metadata, retrieving with various combinations of TopK, MinScore, and MetadataEquals, and verifying the results respect all three filters.

**Acceptance Scenarios**:

1. **Given** an in-memory retriever with 20 indexed chunks, **When** retrieving with `TopK = 5`, **Then** at most 5 chunks are returned, ordered by descending relevance score.
2. **Given** an in-memory retriever with chunks scoring between 0.1 and 0.9, **When** retrieving with `MinScore = 0.5`, **Then** only chunks with score >= 0.5 are returned.
3. **Given** chunks with metadata `{"category": "finance", "region": "EU"}` and `{"category": "finance", "region": "US"}`, **When** retrieving with `MetadataEquals = { "category": "finance", "region": "EU" }`, **Then** only chunks matching both keys are returned.
4. **Given** any retriever, **When** retrieving with a `RetrievalOptions` where all properties are null/default, **Then** the retriever behaves as before (returns results ranked by relevance without additional filtering).

---

### User Story 2 - Provider-Specific Query Extension (Priority: P2)

A library consumer who needs provider-specific retrieval capabilities (e.g. Elasticsearch DSL queries, Azure AI Search OData filters) passes a provider-owned query extension through the common `IRetriever` interface without the interface signature changing.

**Why this priority**: The extension mechanism is the key design enabler for advanced use cases without polluting the common surface.

**Independent Test**: Can be tested by creating a custom retriever that accepts a known extension type, verifying it processes the extension alongside the common options, and verifying that an unsupported extension type produces a clear error.

**Acceptance Scenarios**:

1. **Given** a retriever that supports a custom query extension type, **When** passing that extension via `ProviderQuery`, **Then** the retriever processes the extension together with the common options (AND combination).
2. **Given** a retriever that supports a custom query extension, **When** common options (`MetadataEquals`, `TopK`, `MinScore`) are also provided alongside the extension, **Then** common options are still honoured — the extension does not bypass them.
3. **Given** any retriever, **When** passing a `ProviderQuery` of an unsupported type, **Then** the retriever fails with a clear, descriptive error (not a silent ignore).
4. **Given** a retriever that supports extensions, **When** `ProviderQuery` is null, **Then** the retriever works normally using only the common options.

---

### User Story 3 - Backward-Compatible Pipeline Integration (Priority: P3)

A library consumer using the RAG pipeline (RagPipelineBuilder, ConversationalRagPipeline) or rank fusion continues to work after the IRetriever signature change. The pipeline passes retrieval options through to each configured retriever.

**Why this priority**: Existing pipeline users must not be broken by this change; the pipeline must propagate the new options correctly.

**Independent Test**: Can be tested by building a pipeline with retrievers and verifying that TopK from RagPipelineOptions flows through to the retriever call as part of RetrievalOptions.

**Acceptance Scenarios**:

1. **Given** a RAG pipeline with one or more retrievers, **When** executing a query, **Then** the pipeline passes a `RetrievalOptions` (derived from `RagPipelineOptions.TopK`) to each retriever.
2. **Given** a FakeRetriever from the testing package, **When** called with `RetrievalOptions`, **Then** the captured query includes the full options object for test assertions.

---

### Edge Cases

- What happens when `MetadataEquals` contains a key that does not exist in any chunk's metadata? The chunk does not match (AND semantics — missing key means no equality match).
- What happens when `MetadataEquals` is an empty dictionary? Treated as "no filter" — equivalent to null.
- What happens when `TopK` is null? The retriever uses its own default (implementation-defined).
- What happens when `MinScore` is negative or zero? It is honoured literally — scores below that threshold are excluded (negative threshold effectively means "no filtering").
- What happens when both `TopK` and `MinScore` are set? MinScore filtering is applied first (excluding low-scoring chunks), then TopK limits the remaining results.
- What happens when `ProviderQuery` is a type from a different provider? The retriever fails with an `ArgumentException` naming the unsupported type and the expected type(s).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The library MUST provide an `IRetrievalQueryExtension` marker interface that provider-specific query types implement.
- **FR-002**: The library MUST provide an immutable `RetrievalOptions` record with optional properties: `TopK` (int?), `MinScore` (double?), `MetadataEquals` (IReadOnlyDictionary<string, string>?), and `ProviderQuery` (IRetrievalQueryExtension?).
- **FR-003**: The `IRetriever` interface MUST change its `RetrieveAsync` signature to accept `(string query, RetrievalOptions options, CancellationToken)` instead of `(string query, int topK, CancellationToken)`.
- **FR-004**: Every retriever implementation MUST honour the common options (`TopK`, `MinScore`, `MetadataEquals`) regardless of whether a `ProviderQuery` is present.
- **FR-005**: `MetadataEquals` MUST be a portable AND filter: each key-value pair is matched by string equality against the chunk's metadata, and all pairs must match for a chunk to be included.
- **FR-006**: When a non-null `ProviderQuery` has a type the retriever does not support, the retriever MUST fail with a clear, descriptive error (not silently ignore the extension).
- **FR-007**: When a `ProviderQuery` is present and supported, its conditions MUST be combined with the common options using AND semantics.
- **FR-008**: `MinScore` MUST be defined against the score value in `ScoredChunk`.
- **FR-009**: `InMemoryRetriever` MUST honour `TopK`, `MinScore`, and `MetadataEquals` with AND semantics. It MUST remain safe for concurrent retrieval (snapshot-based reads) and ingestion.
- **FR-010**: The `FakeRetriever` in `Cisharpai.Testing` MUST be updated to accept and capture `RetrievalOptions` instead of `int topK`.
- **FR-011**: All callers of `IRetriever.RetrieveAsync` (pipeline, rank fusion, tests) MUST be updated to pass `RetrievalOptions`.
- **FR-012**: The `RagPipelineOptions.TopK` MUST flow through to the retriever call as part of `RetrievalOptions`.

### Key Entities

- **RetrievalOptions**: Immutable record carrying the common retrieval query surface (TopK, MinScore, MetadataEquals) and an optional provider-specific extension.
- **IRetrievalQueryExtension**: Marker interface for provider-specific query types. Provider packages define concrete types implementing this interface.
- **IRetriever**: The retrieval contract. Its signature changes from `(string, int, CancellationToken)` to `(string, RetrievalOptions, CancellationToken)`.
- **TextChunk.Metadata**: The existing `IReadOnlyDictionary<string, object?>` on each chunk, against which `MetadataEquals` filters are evaluated.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caller using only `RetrievalOptions` with common properties retrieves correct results from any retriever implementation without provider-specific knowledge.
- **SC-002**: A provider-specific query extension travels through the common `IRetriever.RetrieveAsync` method without any signature change beyond the initial migration.
- **SC-003**: Common options (`TopK`, `MinScore`, `MetadataEquals`) are honoured when a provider-specific query is also supplied — verified by tests showing combined AND behavior.
- **SC-004**: An unsupported `ProviderQuery` type produces a descriptive error within one method call — no silent fallback.
- **SC-005**: All existing unit and integration tests pass after the migration, confirming backward-compatible behavior.
- **SC-006**: The breaking API change is documented in RELEASE_NOTES.md, wiki, project overview, and the integrated skill.

## Assumptions

- `MinScore` uses `double` to match the existing `ScoredChunk.Score` type, not `float` as the issue description suggested. This avoids a lossy type mismatch.
- Metadata equality comparison converts chunk metadata values to strings via `?.ToString()` for comparison against `MetadataEquals` string values, since `TextChunk.Metadata` stores `object?` values.
- Embedding and model selection remain outside `RetrievalOptions` — they are configured at retriever construction time, not per-query.
- The `OpenAiFileSearchRetriever` will accept `RetrievalOptions` and honour `TopK` (mapped to `MaxNumResults`). `MinScore` and `MetadataEquals` will be applied as post-filters since the hosted API does not support them natively. `ProviderQuery` support for OpenAI file search is out of scope for this issue.
- Thread safety for `InMemoryRetriever` retrieval is maintained via the existing snapshot pattern (`_store.ToArray()`). The "not thread-safe" disclaimer on concurrent Add/Retrieve remains but concurrent retrieval calls are safe.
