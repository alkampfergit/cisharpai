# Specification: IRagPipeline — Composable Retrieve → Rerank → Pack → Chat Pipeline

**Issue**: #87 | **Date**: 2026-09-21

## Summary

Deliver the single orchestrating abstraction that wires the full RAG flow: query transformation → retrieval → rank fusion → reranking → context packing → grounded chat. Conversation-aware query rewriting is supported via `ConversationQueryRewriter`. Every stage is optional — degenerate pipelines (retrieve-only, chat with pre-packed context) are valid.

## Deliverables

1. **`IRagPipeline`** interface — `AskAsync` and `AskStreamingAsync`
2. **`RagResult`** — answer + citations + full retrieved/packed/dropped sets for debugging
3. **`RagPipelineOptions`** — per-call knobs (conversation history, topK, reranker topN, packing, system prompt, model, temperature, citation mode)
4. **`RagStreamingChunk`** — streaming delta with final result on last chunk
5. **`RagPipelineBuilder`** — fluent builder configuring each stage
6. **`ConversationalRagPipeline`** — default implementation using existing abstractions
7. **`ConversationQueryRewriter`** — wraps `IChatCompletionClient` to rewrite queries given conversation history
8. **DI registration** — `AddCisharpaiRagPipeline()` extension method
9. **`FakeRagPipeline`** + `FakeQueryTransformer`** in `Cisharpai.Testing`
10. **Tests** covering full pipeline, partial pipelines, multi-retriever fusion, streaming, conversation rewriting

## Design decisions

- Immutable records throughout (no mutable `UserQuestion` object)
- Each stage produces an immutable result feeding the next
- `IQueryTransformer` from #88 used for stateless transforms; `ConversationQueryRewriter` handles conversation-aware rewriting
- Grounded chat via `IGroundedChatFeature` when available; fallback via `GroundedChatFallbackHelper` otherwise
- Streaming runs steps 1–5 identically, then streams step 6 via `IStreamingChatFeature`
- RRF fusion when multiple retrievers registered; single retriever passes through
- No forced `Conversation` object — caller manages `List<LlmMessage>` externally

## Acceptance criteria

- [ ] `IRagPipeline` with `AskAsync` and `AskStreamingAsync`
- [ ] Builder pattern with all stages configurable
- [ ] Default implementation using existing abstractions
- [ ] Tests covering: full pipeline, partial pipelines, multi-retriever fusion, streaming
- [ ] `Cisharpai.Testing` fakes for `IRagPipeline` and `IQueryTransformer`
- [ ] `wiki/rag.md`, `RELEASE_NOTES.md`, `memories/project_overview.md` updated
