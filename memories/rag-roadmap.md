# RAG Roadmap

Status date: 2026-09-12. Baseline: `develop` at `9b5804f`, which now carries both the reranker
(#33) and the `Cisharpai.Rag` project (landed by PR #44).

Phase 1 was tracked by epic #43 (issues #37-#42). **Phase 1 is complete** as of 2026-09-13 —
all six children merged and epic #43 is closed. Phase 2 is tracked by epic #55 (issues #56-#61).

## Guiding principle

Cisharpai is a **model-interaction** library, not a data-infrastructure library. The filter
for every item below is:

> Does this wrap a provider API surface, or produce/shape the payload we send to a model?

Everything that answers **yes** is in scope. Storage, indexing, persistence, and file parsing
answer **no** and are explicitly deferred (see "Out of scope").

---

## 1. What exists today

Two separate code bases matter here.

**On `develop`**, there is no chunking: `DocumentChunk` is a grounded-chat *document DTO*,
not a splitter output.

**Landed via PR #44** there is a whole `Cisharpai.Rag` project. It had been stranded on `rag101`
because PR #29's base was that branch, not `develop` — so #28 closed and #29 merged with green
checks while `develop` got nothing. `rag101` has since been **deleted**. It contains:

| Type | What it does |
|---|---|
| `ITextChunker` / `FixedSizeChunker` / `FixedSizeChunkerOptions` | Fixed-size chunking, 1024 Unicode scalars with 128 overlap, UTF-16 source offsets |
| `IBulkEmbeddingProcessor` / `BulkEmbeddingProcessor` / `BulkEmbeddingOptions` | Sequential bounded batches (fixed `BatchSize = 32`), per-batch failure results |
| `IRagIngestionPipeline` / `RagIngestionPipeline` / `RagOptions` | Composes chunking + bulk embedding, streams `EmbeddingBatchResult` |
| `RagDocument`, `TextChunk`, `ChunkEmbedding`, `EmbeddingBatchResult` | Models |

This was Phase 1's blocker (#37, now closed). PR #44 also carried a deliberate Spec Kit upgrade to
1.0.6, plus CI fixes: `.agents/**` excluded from Sonar coverage, `feature/**` dropped from the
`push` trigger to stop duplicate runs, concurrency added, and integration tests gated to trunk
pushes, tags, or a PR labelled `run-integration`.

Here is the rest of the inventory (all on `develop`).

| Capability | Type | Providers | Notes |
|---|---|---|---|
| Text embeddings | `IEmbeddingClient` / `EmbeddingRequest` | OpenAI, Azure OpenAI, Azure AI Inference, Cohere | Has `InputType` (search_query vs search_document) and `Dimensions` — both are RAG-critical and already correct |
| Image embeddings | `IImageEmbeddingFeature` | Azure AI Inference, Cohere | |
| Multimodal embeddings | `IMultimodalEmbeddingFeature` | Cohere (Embed v4) | Matryoshka dimension control |
| Reranking | `IRerankerClient` / `RerankRequest` | Cohere only | `TopN`, `MaxTokensPerDocument`, retargetable `BaseUrl` |
| Grounded chat + citations | `IGroundedChatFeature`, `GroundedChatOptions`, `DocumentChunk`, `Citation`, `CitationSource`, `CitationMode` | All 5 (Anthropic, Cohere, OpenAI, Azure OpenAI native; Azure AI Inference fallback) | Character-offset citations |
| Structured outputs | `IJsonOutputFeature` | All 5 | Needed for extraction + LLM-as-judge |
| Tool calling | `IToolCallingFeature` | All 5 | Needed for agentic retrieval |
| Streaming | `IStreamingChatFeature` | All 5 | |
| Test doubles | `Cisharpai.Testing` | — | `FakeEmbeddingClient`, `FakeRerankerClient`, `FakeChatCompletionClient` |

### Gaps (RAG-relevant)

- ~~`Cisharpai.Rag` is not on `develop`~~ — done, PR #44.
- Chunking is fixed-size only, and sized in Unicode scalars rather than tokens.
- ~~No token counting~~ — **partially wrong, corrected 2026-09-13.** #42 landed
  `BulkEmbeddingOptions.TokenEstimator` (`Func<string, int>?`, default `s => s.Length / 4`). What is
  still missing is *real* counting: a local tokenizer and the provider `count_tokens` / `tokenize`
  endpoints. See the Phase 2 correction note.
- Bulk embedding batching is a hardcoded 32 items with no token budget, no per-provider ceiling,
  no concurrency and no retry (#42).
- No vector math (cosine / normalize / top-k) — you cannot even do an in-memory demo end to end.
- ~~`IGroundedChatFeature` is a 1-of-5 feature~~ — now wired on all 5 providers (Anthropic, Cohere, OpenAI, Azure OpenAI native; Azure AI Inference prompt-injection fallback).
- No prompt caching support — the single biggest cost lever in RAG, and it is pure model interaction.
- No wrapper for provider-hosted retrieval (OpenAI `file_search` / vector stores, Azure OpenAI
  "On Your Data" `data_sources`, Cohere connectors). This is the *right* answer to "do we need a
  vector store abstraction?" — expose the provider's, don't build one.

---

## 2. Timeline

Five phases, ordered by value-per-unit-of-work under the guiding principle. Each phase is
independently shippable and ends with a release-notes line.

### Phase 1 — Close the provider-surface gaps (highest priority)

These are APIs the providers already expose and we simply do not cover. Pure model interaction,
zero new architecture.

1. **Cross-provider grounded chat + citations.**
   Make `IGroundedChatFeature` real on more than Cohere:
   - **Anthropic** — native `document` content blocks with `citations: {enabled: true}`; map
     provider citation spans onto the existing `Citation` start/end/sources shape.
   - **OpenAI / Azure OpenAI** — Responses API annotations.
   - **Everything else** — a prompt-injection fallback (documents serialized into the system
     message with stable ids, citations parsed back out), mirroring how Anthropic JSON mode is
     already implemented. Surface this as a distinct, documented behaviour so callers know when
     grounding is native versus synthesized.

   *Why first:* it is the one RAG feature the library already models but under-delivers, and
   citations are the part of RAG that applications cannot reasonably rebuild themselves.

2. **Prompt caching.**
   Anthropic `cache_control` breakpoints, OpenAI/Azure automatic-cache reporting, Cohere
   equivalents; expose cached-token counts on the response usage. RAG prompts are long and
   repetitive — this is the highest cost/latency ROI item in the whole roadmap.

3. **Harden bulk embedding** (#42, unblocked). The existing `BulkEmbeddingProcessor`
   already reports failures per batch instead of all-or-nothing — the right shape. What it needs
   is token-budget-aware batching, per-provider batch ceilings, bounded concurrency, and retry
   on transient failures. Today an indexing job hits provider limits as an opaque 400 mid-run.

*Deferred out of Phase 1:* **reranking beyond Cohere** (Jina, Voyage, Azure AI Foundry, plus a
chat-model fallback reranker). Real, but lower value than citations and caching — moved to Phase 2.

---

### Phase 2 — The primitives that shape what we send to models

**Correction (2026-09-13).** Section 1 above says "No token counting." That was true when it was
written and is now **wrong**: #42 landed `BulkEmbeddingOptions.TokenEstimator`
(`Func<string, int>?`, defaulting to `s => s.Length / 4`) alongside `MaxBatchTokens`
(`src/Cisharpai.Rag/Embeddings/BulkEmbeddingOptions.cs:10-11`). So Phase 2's token-counting item
is **not greenfield** — it is about backing that crude `Length / 4` heuristic with real counting
and making the existing consumer use it. An `ITokenCounter` built in isolation that leaves the
default estimator orphaned would miss the point of the item.

Three facts verified against current provider/library documentation, each of which constrains
the design:

- **Cohere `POST /v1/tokenize`** takes `{text, model}` and returns `{tokens[], token_strings[]}` —
  string-shaped, count is `tokens.Length`, and the text is capped at **65,536 characters**.
- **Anthropic `POST /v1/messages/count_tokens`** takes a whole `{model, messages, system?, tools?}`
  payload and returns `{input_tokens}` — **message-shaped, not string-shaped.** It answers
  "how big is the request I am about to send", which is a different question from "how many
  tokens is this chunk".
- **`Microsoft.ML.Tokenizers`** gives `TiktokenTokenizer.CreateForModel("gpt-4o").CountTokens(text)`,
  but the tiktoken data files ship as **separate NuGet packages**
  (`Microsoft.ML.Tokenizers.Data.Cl100kBase`, `.O200kBase`, `.P50kBase`) — the main package alone
  fails at runtime. It also exposes `GetIndexByTokenCount` / `GetIndexByTokenCountFromEnd`, which
  is exactly the primitive a token-aware chunker needs. This repo uses central package management,
  so every package added needs a `PackageVersion` entry in `Directory.Packages.props`.

The shape mismatch above is the item's central design fork: a string-shaped synchronous counter
suits the existing `TokenEstimator` and the chunkers, while the provider endpoints are async I/O
and Anthropic's is message-level. Resolution taken below: **`ITokenCounter` is string-shaped and
async; Anthropic's message-level endpoint is deliberately deferred** to the context-packing track
where "size the request I am about to send" actually belongs.

A second fork, in chunking: `ITextChunker.Chunk` is **synchronous**
(`IEnumerable<TextChunk>`, `src/Cisharpai.Rag/Chunking/ITextChunker.cs:7`) and `TextChunk` is
`(DocumentId, Index, StartOffset, Text)` with **no metadata and no end offset**
(`src/Cisharpai.Rag/Models/TextChunk.cs:9`). A semantic chunker needs `IEmbeddingClient`, so it
cannot implement that interface as written. The contract change is therefore split out as its own
work item, done once, before any new chunker is built on top of it.

Work items, in dependency order:

1. **Vector math helpers.** `VectorMath.CosineSimilarity`, `DotProduct`, `Normalize`, `TopK`.
   ~100 lines, no dependencies, and it makes a full RAG demo possible in `Cisharp.Console`
   without anyone taking a database dependency. *No prerequisites.*

2. **Token counting.** `ITokenCounter` (string-shaped, async) in core `Cisharpai`, with a local
   `Microsoft.ML.Tokenizers` implementation and a Cohere `/v1/tokenize` implementation, plus an
   adapter that lets the local counter back `BulkEmbeddingOptions.TokenEstimator` instead of
   `Length / 4`. *No prerequisites.*

3. **Chunking contract.** Make `ITextChunker` async and give `TextChunk` metadata and an end
   offset, so the output feeds `DocumentChunk` and citations cleanly. Migrates `FixedSizeChunker`
   and `RagIngestionPipeline`. Done once, before new chunkers. *No prerequisites.*

4. **Recursive / structure-aware chunker**, sizeable in either characters or tokens
   (paragraph → sentence → word, respecting a real model budget). *Needs 2 and 3.*

5. **Semantic chunker.** Embedding-similarity boundary detection via `IEmbeddingClient`.
   *Needs 1 and 3.*

6. **Context packing.** Given ranked chunks plus a model budget, select and order what actually
   fits — including the lost-in-the-middle ordering heuristic. This is prompt construction:
   squarely in scope. *Needs 2.*

---

### Phase 3 — Provider-hosted retrieval

The strategic answer to "should we abstract a vector store?" — **no, wrap the ones the
providers already run.**

1. **OpenAI Responses API `file_search`** plus the Vector Stores / Files API (create store,
   upload, poll, query) behind an `IHostedRetrievalFeature`.
2. **Azure OpenAI "On Your Data"** (`data_sources` extension: Azure AI Search, Cosmos DB) —
   same feature interface, different transport.
3. **Cohere connectors** on the chat API.
4. **Web-search-as-retrieval** where providers expose it as a server-side tool.

This gives users working retrieval with zero storage code from us, and it is the item most
aligned with the library's mission.

---

### Phase 4 — Orchestration and evaluation

1. **`IRetriever` — a delegate, not an implementation.**
   One small interface (`query + topK → ranked chunks`) that the *application* implements over
   its own store. We ship adapters only where they are model-interaction-shaped (the Phase 3
   hosted retrievers) plus an in-memory implementation for tests and demos.

2. **`IRagPipeline`.** Composes the pieces already built: embed query → `IRetriever` →
   `IRerankerClient` → context packing → `IGroundedChatFeature`, returning answer + citations +
   the retrieved set for debugging. Every stage swappable; every stage optional.

3. **Query transformation helpers.** Query rewriting, multi-query expansion, HyDE,
   step-back prompting — each is a thin, well-prompted model call, which is exactly our layer.

4. **Contextual retrieval.** Anthropic's contextual-chunk-prefixing technique: a batch helper
   that prefixes each chunk with a model-generated summary of its position in the parent
   document, made affordable by Phase 1's prompt caching.

5. **RAG evaluation.** LLM-as-judge scorers for groundedness/faithfulness, answer relevance,
   and context precision/recall, built on `IJsonOutputFeature`. Plus retrieval metrics
   (nDCG, MRR, recall@k) as pure functions.

---

### Phase 5 — Ecosystem polish

`Cisharpai.Testing` fakes for every new abstraction (chunker, token counter, retriever,
hosted retrieval), a full `Cisharp.Console` RAG scenario, and `wiki/rag.md` as the umbrella guide.

*Note:* per `AGENTS.md`, fakes / wiki / `provider-features.md` / `RELEASE_NOTES.md` /
`project_overview.md` / the `cisharpai-expert` skill must be updated **within each phase**,
not deferred to here. Phase 5 is only the cross-cutting demo and umbrella documentation.

---

## 3. Out of scope (deliberately)

| Not doing | Why |
|---|---|
| Vector store abstraction (Qdrant/pgvector/Pinecone/Redis drivers) | Storage, not model interaction. `Microsoft.Extensions.VectorData` already occupies this slot in .NET; we integrate with it at most via `IRetriever`. |
| Document parsers (PDF, DOCX, HTML, OCR) | File-format work with heavy dependencies. Users bring text. |
| Ingestion pipelines / schedulers / change detection | Application infrastructure. |
| Lexical search (BM25) and hybrid fusion | Belongs in the search engine, not in an LLM client. We can expose RRF as a pure function if ranked lists are supplied — nothing more. |
| Persistence / caching of embeddings | Application concern; we only report cache usage the provider gives us. |

---

## 4. Dependency order at a glance

```
Phase 1 COMPLETE (#37 #38 #39 #40 #41 #42 all merged; epic #43 closed)
    |
    +--> Phase 2
    |      vector math ---------------+
    |      token counting ------+      \
    |      chunking contract ---+--+    +--> semantic chunker
    |                            \  \
    |                             \  +--> recursive / token-aware chunker
    |                              +--> context packing
    |        |
    |        +--> Phase 4 (pipeline, query transforms, contextual retrieval, eval)
    |
    +--> Phase 3 (hosted retrieval) ----------^
```

Within Phase 2, three items have no prerequisites and can start in parallel: vector math,
token counting, and the chunking contract change.

Phase 1 has no prerequisites and unlocks the most user value per item. Phase 3 can run in
parallel with Phase 2 — it shares no code with the chunking track.
