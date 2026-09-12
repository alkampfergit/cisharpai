# Feature Specification: Reranker Support

**Feature Branch**: `feature/010-reranker-support`

**Created**: 2026-09-12

**Status**: Approved (issue #24)

**Input**: GitHub issue #24 — "Add an interface with configuration and everything needed to
implement the concept of reranking. Check the actual interface for cohere reranker that is now
the first one I want to implement. Allows also in the implementation to change the base url
because I could want to be able to call it in azure or in other hosting."

## Scope Decisions (from issue discussion)

| Question | Decision |
|----------|----------|
| Which providers? | **Cohere only** for this feature. `CohereClientOptions.BaseUrl` already covers Azure-hosted Cohere deployments. |
| Cohere `priority` field | **Not** part of the unified `RerankRequest`. Reachable through `ExtraParameters`. |
| Console demo | **Yes** — a reranker scenario is added to `Cisharp.Console`. |

Azure AI Inference is explicitly out of scope: Cohere rerank models deployed through Azure AI
Foundry still expose Cohere's own rerank contract, not a unified Azure Model Inference rerank
endpoint. Pointing `BaseUrl` at the Azure deployment is therefore sufficient.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Rerank documents against a query (Priority: P1)

A developer building a RAG pipeline has retrieved a candidate set of documents from a vector
store and wants to reorder them by true relevance to the user's query before feeding the top
few into a prompt. They resolve an `IRerankerClient` and call `RerankAsync` with the query and
the candidate documents, receiving back the documents' original indices paired with relevance
scores, ordered most-relevant first.

**Why this priority**: This is the entire point of the feature — without it nothing else matters.

**Independent Test**: Construct a `CohereRerankerClient` over a mocked HTTP handler, issue a
rerank request, and assert the mapped results carry the expected indices and scores.

**Acceptance Scenarios**:

1. **Given** a configured reranker client, **When** `RerankAsync` is called with a query and
   three documents, **Then** the response has `IsSuccess = true` and one `RerankResult` per
   document scored by the provider.
2. **Given** a request with `TopN = 2` and five documents, **When** the call completes, **Then**
   the provider returns at most two results and `TopN` was sent on the wire.
3. **Given** the provider returns HTTP 401, **When** the call completes, **Then** the response
   has `IsSuccess = false`, a populated `ErrorMessage`, and no exception is thrown.
4. **Given** no `Model` on the request and no `DefaultModel` in options, **When** `RerankAsync`
   is called, **Then** an `InvalidOperationException` is thrown (configuration error, not an
   API error).

---

### User Story 2 - Point the reranker at a non-default host (Priority: P1)

A developer runs Cohere's rerank model on an Azure AI Foundry deployment (or any other host)
rather than `api.cohere.com`. They set `BaseUrl` in the options and everything else — request
shape, response mapping, error handling — stays identical.

**Why this priority**: Explicitly called out in the issue as a requirement, not a nice-to-have.

**Independent Test**: Register the reranker with a custom `BaseUrl` and assert the outgoing
request URI targets that host.

**Acceptance Scenarios**:

1. **Given** options with `BaseUrl = "https://my-host.example/v2/"`, **When** `RerankAsync` is
   called, **Then** the HTTP request targets `https://my-host.example/v2/rerank`.
2. **Given** a custom `BaseUrl` registered through DI, **When** the client is resolved, **Then**
   the configured base address is honoured.

---

### User Story 3 - Register and resolve a reranker like any other client (Priority: P2)

A developer wires the reranker into their application the same way they wire chat and embedding
clients: a `services.AddCohereRerankerClient(...)` extension for direct DI, a keyed overload for
multi-tenant setups, and the runtime `ICisharpaiClientFactory` for configuration-driven
provider selection.

**Why this priority**: Consistency with the existing client surface; without it the feature is
usable but feels bolted on.

**Independent Test**: Build a service collection, register the reranker, resolve
`IRerankerClient`, and assert the concrete type and options.

**Acceptance Scenarios**:

1. **Given** `AddCohereRerankerClient`, **When** `IRerankerClient` is resolved, **Then** a
   `CohereRerankerClient` is returned.
2. **Given** `AddCohereRerankerClient("tenant-a", ...)`, **When** the keyed service is resolved,
   **Then** it is isolated from other keys.
3. **Given** a factory with Cohere support, **When** `CreateRerankerClient` is called with a
   `CohereClientConfiguration`, **Then** a successful result carrying an `IRerankerClient` is
   returned.
4. **Given** a factory with a provider that does not support reranking, **When**
   `CreateRerankerClient` is called, **Then** a failure result explains that reranking is
   unsupported — no exception.

---

### User Story 4 - Test code that depends on reranking (Priority: P2)

A developer writing unit tests for their RAG pipeline needs to stub out reranking without real
API calls. They use `FakeRerankerClient` from `Cisharpai.Testing` to queue canned responses and
inspect the requests their code produced.

**Why this priority**: Mandated by the constitution — the testing package must cover every
client interface.

**Independent Test**: Queue a response on the fake, call it, assert the response and the
captured request.

**Acceptance Scenarios**:

1. **Given** a `FakeRerankerClient` with a queued response, **When** `RerankAsync` is called,
   **Then** the queued response is returned and the request is recorded in `ReceivedRequests`.
2. **Given** a fake with neither queue nor default, **When** called, **Then** an
   `InvalidOperationException` explains that a response must be configured.

---

### User Story 5 - Try reranking from the demo console (Priority: P3)

A developer evaluating the library picks "Cohere rerank" from the console menu, types a query
and a few documents, and sees the ranked output.

**Why this priority**: Discoverability and manual smoke-testing; not required for library users.

**Independent Test**: Run the console, choose the scenario, observe ranked output.

**Acceptance Scenarios**:

1. **Given** `COHERE_API_KEY` is set, **When** the scenario runs, **Then** documents are printed
   in descending relevance order with their scores.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The core package MUST expose an `IRerankerClient` interface with a single
  `RerankAsync(RerankRequest, CancellationToken)` method, implementing `IHasFeatures`.
- **FR-002**: `RerankRequest` MUST be an immutable record carrying `Query`,
  `Documents`, optional `Model`, `TopN`, `MaxTokensPerDocument`, `IncludeRawResponse`, and
  `ExtraParameters`.
- **FR-003**: `RerankResponse` MUST be an immutable record carrying the ranked `Results`, the
  `Model`, optional billing/usage counters, `RawResponseJson`/`RawRequestJson`, and the
  `IsSuccess`/`ErrorMessage` pair, plus a static `Error` factory.
- **FR-004**: `RerankResult` MUST carry the zero-based `Index` into the caller's original
  `Documents` list and a `RelevanceScore`.
- **FR-005**: Results MUST be ordered most-relevant first, as returned by the provider.
- **FR-006**: `CohereRerankerClient` MUST POST to the `rerank` endpoint relative to the
  configured `BaseUrl`, reusing `CohereClientOptions` and `CohereAuthenticationHandler`.
- **FR-007**: API errors MUST surface as `IsSuccess = false` with `ErrorMessage`; only network
  and configuration failures may throw.
- **FR-008**: A missing model (neither on the request nor as `DefaultModel`) MUST throw
  `InvalidOperationException` — it is a configuration error.
- **FR-009**: `ExtraParameters` MUST be deep-merged into the outgoing request body, enabling
  Cohere's `priority` field and any future parameters.
- **FR-010**: `IncludeRawResponse = true` MUST populate both `RawRequestJson` and
  `RawResponseJson`.
- **FR-011**: `ICisharpaiClientFactory` MUST expose `CreateRerankerClient`, and
  `IClientFactoryProvider` MUST expose `SupportsReranking` and `CreateRerankerClient`.
- **FR-012**: Adding reranking to `IClientFactoryProvider` MUST NOT break existing
  implementations — non-supporting providers default to `SupportsReranking = false`.
- **FR-013**: `CohereServiceCollectionExtensions` MUST offer non-keyed and keyed
  `AddCohereRerankerClient` overloads mirroring the chat/embedding registrations.
- **FR-014**: `AddCohereSupport` MUST register the reranker's named `HttpClient` with the
  standard resilience handler.
- **FR-015**: `CohereModels` MUST expose well-known rerank model identifiers.
- **FR-016**: `Cisharpai.Testing` MUST provide `FakeRerankerClient` with queued responses,
  a default response, request capture, and `Reset`; `FakeResponses` MUST offer rerank helpers.
- **FR-017**: `FakeClientFactoryProvider` MUST support reranking so factory-driven code can be
  tested.
- **FR-018**: `Cisharp.Console` MUST include a Cohere rerank scenario.

### Key Entities

- **RerankRequest** — what to rank: a query plus candidate documents, with optional model and
  truncation controls.
- **RerankResponse** — the ranking outcome: ordered results, model, usage, raw payloads, and
  success/error state.
- **RerankResult** — one ranked document: its original index and its relevance score.

## Success Criteria *(mandatory)*

- **SC-001**: A developer can rerank documents through `IRerankerClient` against Cohere using
  only configuration — no provider-specific types in their code.
- **SC-002**: Switching the rerank host to an Azure-hosted deployment requires changing exactly
  one setting (`BaseUrl`).
- **SC-003**: Every API-level failure mode is observable without try/catch.
- **SC-004**: Cohere's `priority` field is reachable without a library change.
- **SC-005**: Downstream consumers can unit-test reranking with no HTTP traffic.
- **SC-006**: All existing `IClientFactoryProvider` implementations continue to compile
  unchanged.
- **SC-007**: The full unit test suite is green on both .NET 8.0 and .NET 10.

## Out of Scope

- An Azure AI Inference reranker provider (no unified Azure rerank endpoint exists).
- Reranking for OpenAI, Anthropic, or Azure OpenAI (no rerank APIs).
- Streaming reranking (not offered by any provider).
- Structured/JSON document reranking (Cohere's document-object form) — plain strings only for
  this iteration; reachable via `ExtraParameters` if needed.
