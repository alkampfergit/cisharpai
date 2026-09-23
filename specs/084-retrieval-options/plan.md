# Implementation Plan: RetrievalOptions with Portable Filters and Provider Query Extensions

**Spec**: [spec.md](spec.md)
**Branch**: `089-retrieval-options`
**Status**: Pending approval

---

## Phase 1 — Core Contracts (FR-001, FR-002, FR-003)

### Task 1.1 — Add `IRetrievalQueryExtension` marker interface

**File**: `src/Cisharpai.Rag/IRetrievalQueryExtension.cs` (new)

Create an empty marker interface in the `Cisharpai.Rag` namespace:

```csharp
public interface IRetrievalQueryExtension { }
```

Provider packages will define concrete types implementing this interface.

**Risk**: None — additive only.

### Task 1.2 — Add `RetrievalOptions` immutable record

**File**: `src/Cisharpai.Rag/RetrievalOptions.cs` (new)

```csharp
public sealed record RetrievalOptions
{
    public int? TopK { get; init; }
    public double? MinScore { get; init; }
    public IReadOnlyDictionary<string, string>? MetadataEquals { get; init; }
    public IRetrievalQueryExtension? ProviderQuery { get; init; }
}
```

Uses `double?` for `MinScore` to match `ScoredChunk.Score` type (see spec design decision).

**Risk**: None — additive only.

### Task 1.3 — Change `IRetriever.RetrieveAsync` signature

**File**: `src/Cisharpai.Rag/IRetriever.cs`

Change from:
```csharp
Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(string query, int topK, CancellationToken cancellationToken = default);
```
To:
```csharp
Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(string query, RetrievalOptions options, CancellationToken cancellationToken = default);
```

**Breaking change**: All implementers and callers must be updated (Phases 2 and 3).

---

## Phase 2 — Update Retriever Implementations (FR-004 through FR-009)

### Task 2.1 — Update `InMemoryRetriever`

**File**: `src/Cisharpai.Rag/InMemoryRetriever.cs`

Changes:
1. Accept `RetrievalOptions` instead of `int topK`.
2. After computing cosine scores via `VectorMath.TopK`, apply filters in order:
   - **MetadataEquals**: Filter chunks where each key-value pair matches via `?.ToString()` equality. Missing key → chunk excluded. Empty/null dict → no filter.
   - **MinScore**: Exclude chunks with `Score < MinScore`. Applied after scoring, before TopK truncation.
   - **TopK**: Pass to `VectorMath.TopK` if set; otherwise use `_store.Count` (return all matches).
3. Reject non-null `ProviderQuery` with `ArgumentException` (InMemoryRetriever supports no extensions).
4. Thread safety: continue using `_store.ToArray()` snapshot pattern — no change needed.

**Implementation detail**: The current code calls `VectorMath.TopK` with a hard `topK`. We need to retrieve more candidates first (all if metadata/minScore filtering is needed), then apply filters, then truncate. Strategy:
- Call `VectorMath.TopK` with `snapshot.Length` (all candidates) when metadata or minScore filters are set, otherwise use `TopK ?? snapshot.Length`.
- Apply metadata filter on the scored results.
- Apply MinScore filter.
- Take first `TopK` (or all if null).

### Task 2.2 — Update `OpenAiFileSearchRetriever`

**File**: `src/Cisharpai.Rag.OpenAi/OpenAiFileSearchRetriever.cs`

Changes:
1. Accept `RetrievalOptions` instead of `int topK`.
2. Map `options.TopK` to `MaxNumResults` in the API request (default to a reasonable value like 10 when null).
3. After getting results, apply `MinScore` as a post-filter.
4. After getting results, apply `MetadataEquals` as a post-filter against chunk metadata (the `attr_*` keys from the result's attributes).
5. Reject non-null `ProviderQuery` with `ArgumentException` (OpenAI file search extension is out of scope per spec).

### Task 2.3 — Update `FakeRetriever`

**File**: `src/Cisharpai.Testing/FakeRetriever.cs`

Changes:
1. Accept `RetrievalOptions` instead of `int topK`.
2. Change `_receivedQueries` from `List<(string Query, int TopK)>` to `List<(string Query, RetrievalOptions Options)>`.
3. Update `ReceivedQueries` property type accordingly.
4. The fake does NOT apply filters — it returns canned responses as-is. This is consistent with the fake pattern (callers test their own logic, not the fake's).

### Task 2.4 — Update `FakeHostedRetrievalFeature`

**File**: `src/Cisharpai.Testing/FakeHostedRetrievalFeature.cs`

Review and update if it returns retrievers — ensure it returns `FakeRetriever` instances with the new signature.

---

## Phase 3 — Update Callers (FR-011, FR-012)

### Task 3.1 — Update `ConversationalRagPipeline.RetrieveAsync`

**File**: `src/Cisharpai.Rag/Pipeline/ConversationalRagPipeline.cs` (lines 262-286)

Change:
```csharp
var results = await retriever.RetrieveAsync(searchQuery, options.TopK, cancellationToken)
```
To:
```csharp
var retrievalOptions = new RetrievalOptions { TopK = options.TopK };
var results = await retriever.RetrieveAsync(searchQuery, retrievalOptions, cancellationToken)
```

Consider whether `RagPipelineOptions` should also expose `MinScore`, `MetadataEquals`, and `ProviderQuery` to flow through. For now, only `TopK` flows through (FR-012). Users who need the full filter surface can call retrievers directly or a future pipeline extension.

### Task 3.2 — Update DI registration tests and helpers

**Files**:
- `src/Cisharpai.Tests/DependencyInjection/OpenAiRagDiRegistrationTests.cs`
- `src/Cisharpai.Testing/FakeServiceCollectionExtensions.cs`

Update any call sites that pass `topK` as an integer to pass `RetrievalOptions` instead.

### Task 3.3 — Update all test files

**Files** (all in `src/Cisharpai.Tests/`):
- `Rag/InMemoryRetrieverTests.cs`
- `Rag/FakeRetrieverTests.cs`
- `RagOpenAi/OpenAiFileSearchRetrievalTests.cs`
- `Testing/FakeHostedRetrievalFeatureTests.cs`
- `OpenAi/OpenAiFileSearchChatPathTests.cs`

Change all `RetrieveAsync(query, topK)` calls to `RetrieveAsync(query, new RetrievalOptions { TopK = topK })`.

---

## Phase 4 — New Tests (FR-005, FR-006, FR-008, FR-009)

### Task 4.1 — `InMemoryRetriever` filter tests

**File**: `src/Cisharpai.Tests/Rag/InMemoryRetrieverTests.cs` (add tests)

New test cases:
- **MetadataEquals AND behavior**: Chunks with different metadata; filter matches only those with all key-value pairs matching.
- **Missing metadata key → exclusion**: Chunk without a filtered key is excluded.
- **Empty MetadataEquals dict → no filter**: Equivalent to null.
- **MinScore filter**: Chunks below threshold excluded; chunks at/above threshold included.
- **TopK truncation**: With more results than TopK, only TopK returned.
- **Null TopK → all results**: Returns all matching chunks.
- **Combined MinScore + TopK**: MinScore applied first, then TopK.
- **Combined MetadataEquals + MinScore + TopK**: All three applied in order.
- **Default RetrievalOptions → same as old behavior**: No filtering, returns all.
- **ProviderQuery rejection**: Non-null ProviderQuery throws `ArgumentException`.
- **Null options properties → no-op**: All null = return everything scored.

### Task 4.2 — `OpenAiFileSearchRetriever` filter tests

**File**: `src/Cisharpai.Tests/RagOpenAi/OpenAiFileSearchRetrievalTests.cs` (add tests)

New test cases:
- **MinScore post-filter**: Mock API returning results with varied scores; MinScore excludes low scorers.
- **MetadataEquals post-filter**: Mock API returning results with attributes; MetadataEquals filters.
- **ProviderQuery rejection**: Non-null ProviderQuery throws `ArgumentException`.
- **Null TopK handling**: Uses sensible default for MaxNumResults.

### Task 4.3 — `FakeRetriever` capture tests

**File**: `src/Cisharpai.Tests/Rag/FakeRetrieverTests.cs` (update + add)

- Verify that `ReceivedQueries` captures full `RetrievalOptions`.
- Verify options with all properties set are captured correctly.

### Task 4.4 — Pipeline integration tests

**File**: `src/Cisharpai.Tests/Rag/` (existing pipeline tests)

- Verify `TopK` from `RagPipelineOptions` flows through to retriever as `RetrievalOptions.TopK`.

---

## Phase 5 — Documentation (SC-006)

### Task 5.1 — Wiki updates

**Files**:
- `wiki/rag-retrieval.md` (or relevant retrieval wiki page) — document `RetrievalOptions`, `IRetrievalQueryExtension`, metadata filtering, MinScore, provider extensions.
- `wiki/provider-features.md` — if retrieval features are tracked there.
- `wiki/testing.md` — document updated `FakeRetriever` API.

### Task 5.2 — Release notes

**File**: `RELEASE_NOTES.md`

Add breaking change entry:
- **Breaking**: `IRetriever.RetrieveAsync` now accepts `RetrievalOptions` instead of `int topK`. Supports portable metadata filtering (`MetadataEquals`), minimum score thresholds (`MinScore`), and provider-specific query extensions (`ProviderQuery`).

### Task 5.3 — Project overview

**File**: `memories/project_overview.md`

Update the RAG section to mention `RetrievalOptions` and the provider-query extension pattern.

### Task 5.4 — Integrated skill

**File**: `llm/cisharpai-expert/SKILL.md` (or equivalent)

Document how users pass retrieval options, metadata filters, and provider extensions.

---

## Dependency Graph

```
Phase 1 (contracts) → Phase 2 (implementations) → Phase 3 (callers) → Phase 4 (new tests)
                                                                      → Phase 5 (docs)
```

Phases 4 and 5 can proceed in parallel after Phase 3.

## Estimated Complexity

- **Phase 1**: Low — 3 small new/modified files
- **Phase 2**: Medium — core filtering logic in InMemoryRetriever, post-filtering in OpenAiFileSearchRetriever
- **Phase 3**: Low — mechanical signature updates
- **Phase 4**: Medium — ~15-20 new test cases
- **Phase 5**: Low — documentation updates

## Risk Mitigation

- **Breaking API change**: All callers are internal to this repo. The `int topK` → `RetrievalOptions` migration is compile-time detectable — any missed call site will fail to build.
- **Thread safety**: InMemoryRetriever's snapshot pattern (`_store.ToArray()`) is unchanged. Filtering happens on the snapshot, not the live store.
- **Score precision**: `MinScore` uses `double` to match `ScoredChunk.Score`. No precision loss.
