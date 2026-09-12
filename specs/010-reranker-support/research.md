# Research: Reranker Support

## R1 — Cohere Rerank v2 API

**Endpoint**: `POST https://api.cohere.com/v2/rerank`

**Request**:

| Field | Type | Notes |
|-------|------|-------|
| `model` | string | required — e.g. `rerank-v3.5` |
| `query` | string | required |
| `documents` | string[] | required — plain strings (object form also accepted) |
| `top_n` | int | optional — how many results to return; defaults to all |
| `max_tokens_per_doc` | int | optional — long documents are truncated to this budget |
| `priority` | int (0–999) | optional scheduling hint — lower = higher priority. **Not** mapped to the unified request (see plan D2) |

**Response**:

```json
{
  "id": "...",
  "results": [
    { "index": 2, "relevance_score": 0.98 },
    { "index": 0, "relevance_score": 0.41 }
  ],
  "meta": { "api_version": {...}, "billed_units": { "search_units": 1 } }
}
```

Results are returned sorted by descending `relevance_score`, and `index` refers to the position
in the submitted `documents` array.

**Decision**: map `results[].index` → `RerankResult.Index` and `results[].relevance_score` →
`RerankResult.RelevanceScore` verbatim, preserving provider ordering.

**Note on the model identifier**: unlike `/v2/embed`, the rerank response's `id` is a request
id, not a model name. The response model is therefore taken from the resolved request model,
not from `id`. (The embedding client maps `id` into `EmbeddingResponse.Model`; we deliberately
do not repeat that here because it would surface a GUID where callers expect a model name.)

## R2 — Rerank models

| Constant | Identifier |
|----------|-----------|
| `RerankV3_5` | `rerank-v3.5` |
| `RerankEnglishV3` | `rerank-english-v3.0` |
| `RerankMultilingualV3` | `rerank-multilingual-v3.0` |

## R3 — Azure / alternative hosting

Cohere rerank models deployed via Azure AI Foundry expose **Cohere's own rerank contract**, not
a unified Azure Model Inference rerank endpoint — the Azure AI Model Inference API has no
rerank operation. Consequently no separate Azure provider is warranted: pointing
`CohereClientOptions.BaseUrl` at the Azure deployment reuses the whole implementation.

Sources consulted during the issue discussion:

- <https://learn.microsoft.com/en-us/azure/ai-studio/how-to/deploy-models-cohere-rerank>
- <https://learn.microsoft.com/en-us/rest/api/aifoundry/modelinference/>
- <https://learn.microsoft.com/en-us/azure/ai-foundry/concepts/models-inference-examples?view=foundry-classic>

**Decision**: Cohere-only implementation; revisit if Microsoft ships a unified rerank endpoint.

## R4 — Extending `IClientFactoryProvider` without breaking consumers

Five in-repo types implement `IClientFactoryProvider` (OpenAI, Anthropic, Azure OpenAI, Azure AI
Inference, Cohere) plus `FakeClientFactoryProvider`. The interface is public, so downstream
consumers may implement it too.

**Options considered**:

1. Add abstract members → breaks every implementation. Rejected.
2. Separate `IRerankerFactoryProvider` interface, discovered by cast → splits the provider model
   and complicates factory lookup. Rejected.
3. **Default interface implementations** (C# 8+, supported on both target frameworks) →
   `SupportsReranking => false` and `CreateRerankerClient => Failure(...)`. Existing
   implementations compile untouched; Cohere overrides both. **Selected.**

## R5 — Existing patterns reused verbatim

- `LlmHttpClient.PostAsync` / `PostWithRawAsync` — serialization, `ExtraParameters` deep-merge,
  logging, telemetry, and `LlmHttpRequestException` on non-2xx.
- `CohereAuthenticationHandler` — bearer auth.
- Snake-case `JsonSerializerOptions` with `WhenWritingNull` — identical to the embedding client,
  so `top_n` and `max_tokens_per_doc` serialize correctly and null optionals are omitted.
- DI registration shape — `AddHttpClient<T>` + auth handler + resilience + transient concrete +
  singleton interface, mirrored for the keyed variant.
