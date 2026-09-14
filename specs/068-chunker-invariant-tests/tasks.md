# Tasks: Property-based invariant tests for all ITextChunker implementations

**Issue**: #68 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

| # | Task | Status |
|---|---|---|
| T1 | Read `FixedSizeChunker`, `RecursiveChunker`, `SemanticChunker`; answer the issue's open question on invariant 2 for each | ✅ |
| T2 | Deterministic xorshift32 generator + 14 document kinds over 5 fixed seeds | ✅ |
| T3 | `ChunkerScenario` table — 35 scenarios across all three chunkers, extensible by one entry | ✅ |
| T4 | Assert invariants 1–4 with seed/kind/options/escaped-text failure messages | ✅ |
| T5 | Wire `SemanticChunker` scenarios to `FakeEmbeddingClient` with deterministic hand-built vectors | ✅ |
| T6 | Run the suite; triage failures (6 `Semantic_*` scenarios failed invariant 2) | ✅ |
| T7 | Fix `SemanticChunker`: anchor first chunk at 0, extend last chunk to `Text.Length` | ✅ |
| T8 | Fix `SemanticChunker`: size backstop measures the emitted span, not the sentence | ✅ |
| T9 | Add `CharacterModeChunkersAgreeOnTheScalarSizingUnit` cross-check | ✅ |
| T10 | Example-based regression tests in `SemanticChunkerTests` for the fixed defects | ✅ |
| T11 | Mutation-test against all three historical defects; widen corpus until each is caught | ✅ |
| T12 | Update `wiki/rag.md` coverage guarantees | ✅ |
| T13 | Update `RELEASE_NOTES.md` and `memories/project_overview.md` | ✅ |
| T14 | Full suite green on `net8.0` and `net10.0` | ✅ |
