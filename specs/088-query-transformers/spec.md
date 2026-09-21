# Specification: Query Transformation Helpers

**Issue**: #88 | **Date**: 2026-09-21

## Summary

Add LLM-based query transformation helpers to `Cisharpai.Rag` that improve retrieval quality. Each transformer implements `IQueryTransformer` with `TransformAsync(string query) → IReadOnlyList<string>`. All take an `IChatCompletionClient` at construction — these are model calls, not rule-based rewrites.

## Deliverables

1. **`IQueryTransformer`** interface in `Cisharpai.Rag.QueryTransformation`
2. **`QueryRewriter`** — reformulates the user query for better retrieval (removes conversational filler, clarifies intent). Returns exactly one output.
3. **`MultiQueryExpander`** — generates N query variants, each retrieved independently, results fused through existing `RankFusion.ReciprocalRank`. Returns original + N variants.
4. **`HydeTransformer`** — Hypothetical Document Embeddings: generates a hypothetical answer, to be embedded instead of the original query. Returns one or more hypothetical documents.
5. **`StepBackTransformer`** — generates a broader, more abstract question to retrieve supporting context. Returns original + step-back question.
6. **`CompositeQueryTransformer`** — chains transformers sequentially, flattening and deduplicating outputs.
7. **`QueryTransformerOptions`** — shared options (Model, Temperature).

## Design decisions

- Transformers are composable (chain via `CompositeQueryTransformer`) and optional in the pipeline.
- All prompts are clear, tested, and provider-agnostic.
- Each transformer accepts a custom system prompt override for domain-specific tuning.
- `includeOriginal` flag controls whether the original query appears in the output.
- Errors from the LLM throw `InvalidOperationException`; empty/whitespace responses fall back to the original query.
- IRagPipeline integration (#87) deferred — these are standalone composable components.

## Acceptance criteria

- [x] `IQueryTransformer` interface in `Cisharpai.Rag`
- [x] All four implementations with tests using `FakeChatCompletionClient`
- [x] `CompositeQueryTransformer` for chaining with tests
- [x] `wiki/rag.md`, `RELEASE_NOTES.md` updated
- [ ] Integration with `IRagPipeline` (#87) — deferred until #87 lands
