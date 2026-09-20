# Tasks: Reranker Support

**Feature**: `feature/010-reranker-support` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

`[P]` marks tasks that touch disjoint files and can run in parallel.

## Phase 1 — Core abstraction (US1)

- [x] **T001** [P] Add `src/Cisharpai/Models/RerankRequest.cs` — immutable record per data-model.
- [x] **T002** [P] Add `src/Cisharpai/Models/RerankResult.cs` — `Index` + `RelevanceScore`.
- [x] **T003** [P] Add `src/Cisharpai/Models/RerankResponse.cs` — results, model, usage, raw
  payloads, `IsSuccess`/`ErrorMessage`, static `Error` factory.
- [x] **T004** Add `src/Cisharpai/IRerankerClient.cs` — `IHasFeatures` + `RerankAsync`.

## Phase 2 — Cohere implementation (US1, US2)

- [x] **T005** [P] Add `src/Cisharpai.Cohere/Models/CohereRerankRequest.cs`.
- [x] **T006** [P] Add `src/Cisharpai.Cohere/Models/CohereRerankResponse.cs` (results + meta /
  billed units).
- [x] **T007** Add `src/Cisharpai.Cohere/CohereRerankerClient.cs` — POST to `rerank`, model
  resolution, `ExtraParameters` merge, raw capture, error mapping, static `Create` helper.
- [x] **T008** Extend `CohereModels` with a `Rerank` class of well-known model ids.

## Phase 3 — DI and factory wiring (US3)

- [x] **T009** Add `SupportsReranking` and `CreateRerankerClient` as **default** members on
  `IClientFactoryProvider` (no breaking change).
- [x] **T010** Add `CreateRerankerClient` to `ICisharpaiClientFactory` and implement it in
  `CisharpaiClientFactory` with registered/unsupported failure handling.
- [x] **T011** Add non-keyed and keyed `AddCohereRerankerClient` overloads to
  `CohereServiceCollectionExtensions`.
- [x] **T012** Register the rerank named `HttpClient` in `CohereFactoryBuilderExtensions` and
  implement rerank support in `CohereClientFactoryProvider`.

## Phase 4 — Testing package (US4)

- [x] **T013** Add `src/Cisharpai.Testing/FakeRerankerClient.cs` — queue, default, capture,
  `Reset`.
- [x] **T014** Extend `FakeResponses` with `Rerank` / `RerankError` helpers.
- [x] **T015** Add rerank support to `FakeClientFactoryProvider` and register the fake reranker
  in `FakeServiceCollectionExtensions`.

## Phase 5 — Console demo (US5)

- [x] **T016** Add `src/Cisharp.Console/Scenarios/CohereRerankScenario.cs`.

## Phase 6 — Tests

- [x] **T017** [P] `src/Cisharpai.Tests/Cohere/CohereRerankerClientTests.cs` — request mapping,
  endpoint, custom base URL, `TopN`, `MaxTokensPerDocument`, `ExtraParameters` (`priority`),
  response mapping and ordering, raw payloads, HTTP error handling, missing-model throw,
  `DefaultModel` fallback.
- [x] **T018** [P] `src/Cisharpai.Tests/Testing/FakeRerankerClientTests.cs`.
- [x] **T019** [P] Extend `src/Cisharpai.Tests/DependencyInjection/CohereDiRegistrationTests.cs`
  with non-keyed and keyed reranker cases.
- [x] **T020** [P] Extend `src/Cisharpai.Tests/Factory/CohereFactoryTests.cs` and
  `CisharpaiClientFactoryTests.cs` with reranker creation and unsupported-provider cases.
- [x] **T021** Add `src/Cisharpai.Integration.Tests/Cohere/CohereRerankIntegrationTests.cs`
  (.NET 10 only, reuses `COHERE_TEST_API_KEY`).

## Phase 7 — Documentation

- [x] **T022** [P] Add `wiki/reranking.md`.
- [x] **T023** [P] Update `wiki/index.md` TOC and `wiki/provider-features.md` matrix.
- [x] **T024** [P] Update `wiki/testing.md` with `FakeRerankerClient`.
- [x] **T025** [P] Update `RELEASE_NOTES.md`.
- [x] **T026** [P] Update `memories/project_overview.md`.
- [x] **T027** [P] Update `llm/cisharpai-expert/SKILL.md` with reranking usage.

## Phase 8 — Verification

- [x] **T028** `dotnet build` and `dotnet test` green on .NET 8.0 and .NET 10.
