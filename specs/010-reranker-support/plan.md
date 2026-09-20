# Implementation Plan: Reranker Support

**Branch**: `feature/010-reranker-support` | **Date**: 2026-09-12 | **Spec**: [spec.md](./spec.md)

## Summary

Add a unified reranking abstraction (`IRerankerClient` + `RerankRequest`/`RerankResponse`/
`RerankResult`) to the core package and implement it for Cohere against the `POST /v2/rerank`
endpoint. Wire it through DI, the runtime client factory, and the testing fakes, and add a
console demo scenario. Host flexibility comes free from the existing configurable
`CohereClientOptions.BaseUrl`.

## Technical Context

**Language/Version**: C# / .NET 8.0 and .NET 10 (multi-target)

**Primary Dependencies**: `System.Text.Json`, `Microsoft.Extensions.Http`,
`Microsoft.Extensions.DependencyInjection`

**Testing**: NUnit + NSubstitute; `MockHttpMessageHandler` for wire-level assertions

**Project Type**: Class library (NuGet) + demo console

**Constraints**: No provider SDKs; no breaking changes to public interfaces

## Constitution Check

| Principle | How this feature complies |
|-----------|---------------------------|
| I. Unified Abstraction | `IRerankerClient` lives in the core package; Cohere is one implementation behind it. Client implements `IHasFeatures` so future optional rerank capabilities follow the feature-collection pattern. |
| II. No Exceptions for API Errors | `RerankResponse.Error(...)` is returned for every `LlmHttpRequestException` and mapping failure. `InvalidOperationException` is reserved for the missing-model configuration error. |
| III. Debuggability First | `IncludeRawResponse` routes through `LlmHttpClient.PostWithRawAsync`, populating both raw payloads. `ExtraParameters` deep-merges into the request body. |
| IV. Immutability | All three DTOs are `sealed record`s. |
| V. Test-Driven Quality | Unit tests for the client, DI, factory, and fake; `FakeRerankerClient` added to `Cisharpai.Testing`. |
| VI. Multi-Target | No framework-specific APIs; nothing added to the csproj files. |
| VII. Documentation as Deliverable | `wiki/reranking.md` (new), `wiki/index.md`, `wiki/provider-features.md`, `wiki/testing.md`, `RELEASE_NOTES.md`, `memories/project_overview.md`, and the `cisharpai-expert` skill. |

**Gate result**: PASS — no deviations, no complexity-tracking entries needed.

## Key Design Decisions

### D1 — Default interface members for the factory provider

`IClientFactoryProvider` is public API implemented by five in-repo providers and potentially by
downstream code. Adding two abstract members would be a breaking change. Instead
`SupportsReranking` and `CreateRerankerClient` ship as **default interface implementations**
(`=> false` and a `Failure(...)` result respectively), so:

- Existing implementations compile untouched (SC-006).
- Cohere and the test fake override them explicitly.
- `CisharpaiClientFactory` gets the same "not registered" / "does not support" failure
  treatment already used for chat and embeddings.

### D2 — `priority` stays out of the unified request

Per the issue discussion. Cohere's `priority` is a scheduling hint with no analogue at other
providers; putting it in `RerankRequest` would leak a provider concept into the abstraction.
`ExtraParameters` already deep-merges arbitrary JSON, so
`JsonSerializer.SerializeToElement(new { priority = 500 })` covers it (integer 0–999, lower = higher priority).

### D3 — Index semantics

Cohere returns `{ index, relevance_score }` where `index` points into the submitted document
array. We pass that through unchanged: `RerankResult.Index` is the caller's original index.
This keeps the caller's document list as the source of truth and avoids duplicating document
text in the response.

### D4 — Usage counters

Cohere's rerank `meta.billed_units` reports `search_units`, while other/future providers may
report tokens. Both are exposed as nullable (`SearchUnits`, `InputTokens`) rather than forcing
a single "tokens" number that would be wrong for Cohere.

### D5 — Model resolution

Identical to the embedding client: `request.Model ?? options.DefaultModel ?? throw`. Reusing
the existing pattern keeps behaviour predictable across clients.

## Project Structure

### Documentation (this feature)

```text
specs/010-reranker-support/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── tasks.md
└── contracts/
    └── cohere-rerank-v2.md
```

### Source changes

```text
src/Cisharpai/
├── IRerankerClient.cs                      (new)
├── ICisharpaiClientFactory.cs              (+ CreateRerankerClient)
├── IClientFactoryProvider.cs               (+ default members)
├── CisharpaiClientFactory.cs               (+ CreateRerankerClient)
└── Models/
    ├── RerankRequest.cs                    (new)
    ├── RerankResponse.cs                   (new)
    └── RerankResult.cs                     (new)

src/Cisharpai.Cohere/
├── CohereRerankerClient.cs                 (new)
├── CohereModels.cs                         (+ Rerank constants)
├── CohereServiceCollectionExtensions.cs    (+ 2 overloads)
├── CohereFactoryBuilderExtensions.cs       (+ rerank HttpClient)
├── CohereClientFactoryProvider.cs          (+ rerank support)
└── Models/
    ├── CohereRerankRequest.cs              (new)
    └── CohereRerankResponse.cs             (new)

src/Cisharpai.Testing/
├── FakeRerankerClient.cs                   (new)
├── FakeResponses.cs                        (+ Rerank helpers)
├── FakeClientFactoryProvider.cs            (+ rerank support)
└── FakeServiceCollectionExtensions.cs      (+ fake reranker registration)

src/Cisharp.Console/Scenarios/
└── CohereRerankScenario.cs                 (new)

src/Cisharpai.Tests/
├── Cohere/CohereRerankerClientTests.cs     (new)
├── DependencyInjection/CohereDiRegistrationTests.cs  (+ rerank cases)
├── Factory/CohereFactoryTests.cs           (+ rerank cases)
└── Testing/FakeRerankerClientTests.cs      (new)

src/Cisharpai.Integration.Tests/Cohere/
└── CohereRerankIntegrationTests.cs         (new, .NET 10 only)
```

No new environment variables — the integration test reuses `COHERE_TEST_API_KEY`.

## Testing Strategy

| Layer | What it covers |
|-------|----------------|
| Wire-level unit tests (`MockHttpMessageHandler`) | Request mapping, endpoint path, custom `BaseUrl`, `TopN`/`MaxTokensPerDocument` serialization, `ExtraParameters` merge (incl. `priority`), response mapping, ordering, raw payload capture, HTTP error → `IsSuccess=false`, missing model → throw |
| DI tests | Non-keyed and keyed registration, options isolation, resolution as `IRerankerClient` |
| Factory tests | Cohere support flag, successful creation, wrong-configuration failure, unsupported-provider failure |
| Fake tests | Queue, default, capture, reset, no-response error |
| Integration test (.NET 10) | Real Cohere rerank call, skipped when `COHERE_TEST_API_KEY` is absent |

## Risks

| Risk | Mitigation |
|------|------------|
| Cohere rerank response shape drifts | Raw JSON is always available; `ExtraParameters` covers request-side drift |
| Downstream implementers of `IClientFactoryProvider` | Default interface members keep them source-compatible (D1) |
| Azure-hosted Cohere path differences | `BaseUrl` is fully caller-controlled; the endpoint is appended relatively |
