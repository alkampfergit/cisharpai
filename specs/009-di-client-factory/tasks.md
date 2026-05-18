# Tasks: DI Client Factory

**Input**: Design documents from `/specs/009-di-client-factory/`

**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/factory-api.md, quickstart.md

**Tests**: Tests are required per spec — the spec explicitly mandates unit test coverage for all provider factory paths (SC-004).

**Organization**: Tasks are grouped by implementation phase, mapping to user stories US1 (registration), US2 (chat factory), US3 (embedding factory), US4 (configuration). US4 is foundational to all others, US1+US2 are P1 (MVP), US3 is P2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Exact file paths included in descriptions

---

## Phase 1: Foundational — Core Factory Types (Cisharpai/)

**Purpose**: Core abstractions that MUST be complete before ANY provider can be registered. All new types live in the existing `src/Cisharpai/` project.

**⚠️ CRITICAL**: No provider or user story work can begin until this phase is complete.

- [x] T001 [P] Create `CisharpaiProvider` enum (OpenAi, AzureOpenAi, AzureAiInference, Anthropic, Cohere) in `src/Cisharpai/CisharpaiProvider.cs`
- [x] T002 [P] Create `CisharpaiClientConfiguration` abstract record (Provider + ApiKey) in `src/Cisharpai/CisharpaiClientConfiguration.cs`
- [x] T003 [P] Create `CisharpaiClientFactoryResult<T>` immutable record with `IsSuccess`, `Client`, `ErrorMessage`, and static `Success()`/`Failure()` factory methods in `src/Cisharpai/CisharpaiClientFactoryResult.cs`
- [x] T004 [P] Create `IClientFactoryProvider` interface (Provider property, SupportsChatCompletion, SupportsEmbedding, CreateChatCompletionClient, CreateEmbeddingClient) in `src/Cisharpai/IClientFactoryProvider.cs`
- [x] T005 [P] Create `ICisharpaiClientFactory` interface (CreateChatCompletionClient, CreateEmbeddingClient, GetRegisteredProviders) in `src/Cisharpai/ICisharpaiClientFactory.cs`
- [x] T006 [P] Create `ICisharpaiClientFactoryBuilder` interface (AddProvider, Services property) in `src/Cisharpai/ICisharpaiClientFactoryBuilder.cs`
- [x] T007 Create `CisharpaiClientFactory` default implementation (routes on Provider enum, delegates to registered IClientFactoryProvider instances, returns error result for unregistered/unsupported) in `src/Cisharpai/CisharpaiClientFactory.cs` — depends on T001–T006
- [x] T008 Create `CisharpaiClientFactoryExtensions` with `AddCisharpaiClientFactory()` IServiceCollection extension that registers the factory and returns the builder in `src/Cisharpai/CisharpaiClientFactoryExtensions.cs` — depends on T007

**Checkpoint**: Core factory infrastructure ready — provider implementations can now begin.

---

## Phase 2: US4 + US1 + US2 — OpenAI Provider (P1) 🎯 MVP

**Goal**: First complete provider — proves the factory pattern end-to-end with chat + embedding.

**Independent Test**: Register OpenAI support, create chat and embedding clients from configuration, verify they use DI-managed HttpClient with resilience handler.

### Tests for OpenAI Provider

- [x] T009 [P] [US4] Create `ClientConfigurationTests` testing CisharpaiClientConfiguration base and CisharpaiClientFactoryResult<T> in `src/Cisharpai.Tests/Factory/ClientConfigurationTests.cs`
- [x] T010 [P] [US2] Create `CisharpaiClientFactoryTests` testing factory registration, routing, error cases (unregistered provider, unsupported interface, wrong config type) in `src/Cisharpai.Tests/Factory/CisharpaiClientFactoryTests.cs`
- [x] T011 [P] [US1+US2] Create `OpenAiFactoryTests` testing OpenAI registration, chat client creation, embedding client creation, HttpClient uses resilience handler in `src/Cisharpai.Tests/Factory/OpenAiFactoryTests.cs`

### Implementation for OpenAI Provider

- [x] T012 [US4] Create `OpenAiClientConfiguration` record extending CisharpaiClientConfiguration with BaseUrl, DefaultModel, Organization, ReasoningEffort, TextVerbosity in `src/Cisharpai.OpenAi/OpenAiClientConfiguration.cs`
- [x] T013 [US1+US2] Create `OpenAiClientFactoryProvider` implementing IClientFactoryProvider — creates OpenAiChatCompletionClient and OpenAiEmbeddingClient from OpenAiClientConfiguration using IHttpClientFactory with named client and resilience handler in `src/Cisharpai.OpenAi/OpenAiClientFactoryProvider.cs`
- [x] T014 [US1] Create `OpenAiFactoryBuilderExtensions` with `AddOpenAiSupport()` extension on ICisharpaiClientFactoryBuilder — registers named HttpClient with auth handler + resilience, registers OpenAiClientFactoryProvider in `src/Cisharpai.OpenAi/OpenAiFactoryBuilderExtensions.cs`
- [x] T015 [US2] Run tests and verify OpenAI chat factory path produces a functional client identical in behavior to the existing `AddOpenAiClient()` DI registration

**Checkpoint**: OpenAI provider fully functional — factory creates chat and embedding clients with resilience. MVP is testable.

---

## Phase 3: US1 + US2 + US4 — Anthropic Provider (P1)

**Goal**: Anthropic provider — chat only (no embedding support). Verifies the "provider doesn't support embedding" error path.

**Independent Test**: Register Anthropic support, create chat client, verify embedding request returns IsSuccess=false with descriptive error.

### Tests

- [x] T016 [P] [US1+US2] Create `AnthropicFactoryTests` testing registration, chat client creation, embedding-not-supported error in `src/Cisharpai.Tests/Factory/AnthropicFactoryTests.cs`

### Implementation

- [x] T017 [P] [US4] Create `AnthropicClientConfiguration` record extending CisharpaiClientConfiguration with BaseUrl, ApiVersion, DefaultModel in `src/Cisharpai.Anthropic/AnthropicClientConfiguration.cs`
- [x] T018 [US1+US2] Create `AnthropicClientFactoryProvider` implementing IClientFactoryProvider — creates AnthropicChatCompletionClient, returns failure for embedding in `src/Cisharpai.Anthropic/AnthropicClientFactoryProvider.cs`
- [x] T019 [US1] Create `AnthropicFactoryBuilderExtensions` with `AddAnthropicSupport()` in `src/Cisharpai.Anthropic/AnthropicFactoryBuilderExtensions.cs`

**Checkpoint**: Anthropic factory path works. "No embedding" error path verified.

---

## Phase 4: US1 + US2 + US3 + US4 — Azure Providers (P1)

**Goal**: Both Azure providers (Azure OpenAI + Azure AI Inference) — both support chat and embedding.

**Independent Test**: Register each Azure provider, create chat and embedding clients with Azure-specific config (Endpoint, DeploymentName, etc.).

### Tests

- [x] T020 [P] [US1+US2+US3] Create `AzureOpenAiFactoryTests` testing registration, chat + embedding creation with AzureOpenAiClientConfiguration in `src/Cisharpai.Tests/Factory/AzureOpenAiFactoryTests.cs`
- [x] T021 [P] [US1+US2+US3] Create `AzureAiInferenceFactoryTests` testing registration, chat + embedding creation with AzureAiInferenceClientConfiguration in `src/Cisharpai.Tests/Factory/AzureAiInferenceFactoryTests.cs`

### Implementation

- [x] T022 [P] [US4] Create `AzureOpenAiClientConfiguration` record with Endpoint, DeploymentName, ApiVersion, DefaultModel, ModelName, ReasoningEffort, TextVerbosity in `src/Cisharpai.Azure/AzureOpenAi/AzureOpenAiClientConfiguration.cs`
- [x] T023 [P] [US4] Create `AzureAiInferenceClientConfiguration` record with Endpoint, ApiVersion, ModelId in `src/Cisharpai.Azure/AzureAiInference/AzureAiInferenceClientConfiguration.cs`
- [x] T024 [US1+US2+US3] Create `AzureOpenAiClientFactoryProvider` implementing IClientFactoryProvider — creates AzureOpenAiChatCompletionClient + AzureOpenAiEmbeddingClient in `src/Cisharpai.Azure/AzureOpenAi/AzureOpenAiClientFactoryProvider.cs`
- [x] T025 [US1+US2+US3] Create `AzureAiInferenceClientFactoryProvider` implementing IClientFactoryProvider — creates AzureAiInferenceChatCompletionClient + AzureAiInferenceEmbeddingClient in `src/Cisharpai.Azure/AzureAiInference/AzureAiInferenceClientFactoryProvider.cs`
- [x] T026 [US1] Create `AzureFactoryBuilderExtensions` with `AddAzureOpenAiSupport()` and `AddAzureAiInferenceSupport()` in `src/Cisharpai.Azure/Extensions/AzureFactoryBuilderExtensions.cs`

**Checkpoint**: Both Azure providers produce chat and embedding clients. All 3 providers (OpenAI + 2 Azure) complete.

---

## Phase 5: US1 + US2 + US3 + US4 — Cohere Provider (P1)

**Goal**: Cohere provider — chat + embedding. Completes all 5 providers (SC-003).

**Independent Test**: Register Cohere support, create chat and embedding clients.

### Tests

- [x] T027 [P] [US1+US2+US3] Create `CohereFactoryTests` testing registration, chat + embedding creation with CohereClientConfiguration in `src/Cisharpai.Tests/Factory/CohereFactoryTests.cs`

### Implementation

- [x] T028 [P] [US4] Create `CohereClientConfiguration` record with BaseUrl, DefaultModel in `src/Cisharpai.Cohere/CohereClientConfiguration.cs`
- [x] T029 [US1+US2+US3] Create `CohereClientFactoryProvider` implementing IClientFactoryProvider — creates CohereChatCompletionClient + CohereEmbeddingClient in `src/Cisharpai.Cohere/CohereClientFactoryProvider.cs`
- [x] T030 [US1] Create `CohereFactoryBuilderExtensions` with `AddCohereSupport()` in `src/Cisharpai.Cohere/CohereFactoryBuilderExtensions.cs`

**Checkpoint**: All 5 providers registered and functional. SC-003 met.

---

## Phase 6: Testing Project — Fake Factory Provider

**Goal**: Add factory support to `Cisharpai.Testing` for consumers writing unit tests.

- [x] T031 [P] Create `FakeClientFactoryProvider` with queue-based chat/embedding responses in `src/Cisharpai.Testing/FakeClientFactoryProvider.cs`
- [x] T032 [P] Create `FakeFactoryBuilderExtensions` with `AddFakeSupport()` in `src/Cisharpai.Testing/FakeFactoryBuilderExtensions.cs`
- [x] T033 Create `FakeClientFactoryTests` verifying fake provider queues and factory integration in `src/Cisharpai.Tests/Factory/FakeClientFactoryTests.cs`

**Checkpoint**: Testing project supports factory pattern. Consumers can unit-test factory-dependent code.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, demo, and post-change checklist items

- [x] T034 [P] Update `wiki/provider-features.md` — add "Factory Support" row to provider matrix
- [x] T035 [P] Create `wiki/factory.md` — factory usage guide with registration, runtime creation, configuration, error handling examples
- [x] T036 [P] Update `wiki/index.md` — add Factory link to TOC
- [x] T037 [P] Update `wiki/testing.md` — document FakeClientFactoryProvider usage
- [x] T038 [P] Update `RELEASE_NOTES.md` — add one line for DI client factory feature
- [x] T039 [P] Update `memories/project_overview.md` — add factory types to project structure
- [x] T040 [P] Update `llm/cisharpai-expert/SKILL.md` — document factory feature for AI-assisted usage
- [x] T041 Add factory demo to Console app (`src/Cisharp.Console/`) — menu option showing factory registration + runtime client creation with Spectre.Console
- [x] T042 Run full test suite (`dotnet test`) across both target frameworks (net8.0, net10.0) and verify all tests pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Foundational)**: No dependencies — start immediately
- **Phase 2 (OpenAI/MVP)**: Depends on Phase 1 completion — BLOCKS on core types
- **Phase 3 (Anthropic)**: Depends on Phase 1. Can run in PARALLEL with Phase 2 if core types are done.
- **Phase 4 (Azure)**: Depends on Phase 1. Can run in PARALLEL with Phases 2+3.
- **Phase 5 (Cohere)**: Depends on Phase 1. Can run in PARALLEL with Phases 2+3+4.
- **Phase 6 (Testing)**: Depends on Phase 1 (core types). Can run in parallel with provider phases.
- **Phase 7 (Polish)**: Depends on Phases 1–6 completion.

### User Story Coverage

| User Story | Phase(s) | Tasks |
|-----------|---------|-------|
| US1 - Registration | 1, 2, 3, 4, 5 | T001–T008, T014, T019, T026, T030 |
| US2 - Chat Client | 2, 3, 4, 5 | T010–T015, T016–T018, T020–T025, T027–T029 |
| US3 - Embedding Client | 2, 4, 5 | T011, T020–T021, T024–T025, T027, T029 |
| US4 - Configuration | 1, 2, 3, 4, 5 | T001–T003, T009, T012, T017, T022–T023, T028 |

### Parallel Opportunities

After Phase 1 completes, Phases 2–6 can all run in parallel since each provider lives in a separate project and touches different files:
- `src/Cisharpai.OpenAi/` — Phase 2
- `src/Cisharpai.Anthropic/` — Phase 3
- `src/Cisharpai.Azure/` — Phase 4
- `src/Cisharpai.Cohere/` — Phase 5
- `src/Cisharpai.Testing/` — Phase 6

Within Phase 1, T001–T006 are all parallel (separate files). T007 depends on T001–T006. T008 depends on T007.

Within each provider phase, config class + test file are parallel, then factory provider depends on config, then builder extensions depend on factory provider.

---

## Implementation Strategy

### MVP First (Phase 1 + Phase 2 Only)

1. Complete Phase 1: Core factory types in `Cisharpai/`
2. Complete Phase 2: OpenAI provider + tests
3. **STOP and VALIDATE**: Run OpenAI factory tests, verify chat + embedding paths
4. Confirm the pattern works before replicating to other providers

### Incremental Delivery

1. Phase 1 → Foundation ready
2. Phase 2 → OpenAI works (MVP — SC-001 proven with one provider)
3. Phase 3 → Anthropic + "no embedding" error path
4. Phase 4 → Azure providers (2 more)
5. Phase 5 → Cohere (all 5 complete — SC-003 met)
6. Phase 6 → Testing project updated
7. Phase 7 → Docs, demo, post-change checklist

### Sequential Recommended Path

For a single implementer, phases 2–5 are best done sequentially (OpenAI → Anthropic → Azure → Cohere) since the pattern from OpenAI is replicated. Each subsequent provider is faster.
