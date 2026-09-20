# Spec: Property-based invariant tests for all ITextChunker implementations

**Issue**: #68 | **Branch**: `feature/068-chunker-invariant-tests` | **Date**: 2026-09-14

## Problem

Three separate defects in RAG Phase 2 were the same mistake — a size bound checked against something other than what actually gets emitted — and all three were caught by a reviewer reading code, never by a test:

- **#60 / PR #65** — `SemanticChunker` summed sentence lengths for its size backstop, ignoring the inter-sentence separators that end up in the chunk.
- **#60 / PR #65** — the same chunker let a single oversized sentence through, because the limit was only consulted when *adding* a subsequent sentence.
- **#59 / PR #67** — `RecursiveChunker` sized raw ranges to `MaxChunkSize` and *then* extended them backwards for overlap, producing over-budget chunks. The fix introduced a second defect where the budget cap pushed a chunk's start past the previous chunk's end and silently dropped source text.

The existing suites are example-based: they check the boundary cases someone thought of. None asserts the guarantees hold *in general*.

## The invariants

For any chunker, any document, any valid options, every emitted chunk set must satisfy:

1. **Verbatim contract.** `chunk.Text == document.Text[chunk.StartOffset..chunk.EndOffset]`. All chunkers shipped today are built-in/verbatim.
2. **Full coverage, no gaps.** Ordered by `StartOffset`, the ranges cover `[0, document.Text.Length)` with no hole between consecutive chunks. Overlap is allowed — this asserts nothing is *lost*, not that nothing repeats.
3. **Size bound.** Every chunk's size, measured in the chunker's own unit, is `<= MaxChunkSize`, except in configurations documented to emit oversized chunks.
4. **Offsets are sane.** `0 <= StartOffset < EndOffset <= Text.Length`, indices contiguous from zero, no empty chunk.

## Answer to the issue's open question — does invariant 2 hold unconditionally?

**Yes for `FixedSizeChunker` and `RecursiveChunker`. No for `SemanticChunker` as shipped — and that was a defect, not a design decision, so it is fixed here rather than encoded as an exception.**

### `RecursiveChunker` — gap-free, confirmed

`FindContentStarts` records `pos + sep.Length` as each piece start, so separator text is attached to the *preceding* piece. `MergePiecesAsync` emits `(contentStarts[chunkFrom], contentStarts[i])` and the next chunk starts at exactly `contentStarts[i]`. `HardCutAsync` advances `pos` to `cutEnd` with no gap and the final piece extends to `regionEnd`. `ApplyOverlap` only ever moves a chunk's start *backwards*, clamped to `chunk.Start`, so coverage cannot open a hole. No separator text is dropped.

### `FixedSizeChunker` — gap-free by construction

`step = ChunkSize - Overlap`, `start` advances by `step` scalars, the last chunk extends to `text.Length`.

### `SemanticChunker` — two real gaps, now fixed

`MakeChunk` extended non-last chunks to `sentences[next].StartOffset`, so inter-sentence whitespace was correctly kept. But:

1. The last chunk ended at `sentences[last].EndOffset`, not `document.Text.Length` — trailing whitespace, or any tail the splitter rejected, was **silently dropped**.
2. The first chunk started at `sentences[0].StartOffset`, so leading text the splitter did not claim was **silently dropped**.

The property suite surfaced (1) immediately, on all six `SemanticChunker` scenarios.

### The one legitimate "no chunks" case

`SemanticChunker` emits nothing when the sentence splitter finds no sentences (a whitespace-only document). That is intended — there is no retrievable content — and is not a weakening of invariant 2: the rule is *"if the splitter finds sentences, the chunks cover the document exactly; if it finds none, there are no chunks"*, and the suite asserts exactly that, cross-checking the splitter rather than accepting an empty result.

## Changes

### New: `src/Cisharpai.Tests/Rag/ChunkerInvariantPropertyTests.cs`

- **No new test dependency.** A hand-rolled deterministic xorshift32 generator over a fixed seed list (`1, 7, 42, 1337, 20260914`), not FsCheck. Chosen over `System.Random` so the corpus is byte-identical across .NET 8 and .NET 10. Failures print the seed, document kind, scenario options and the source text escaped as an ASCII-safe C# literal.
- **14 document kinds**, including non-BMP coverage: `empty`, `shorter-than-chunk`, `ascii-prose`, `emoji-dense`, `cjk`, `mixed-scripts`, `whitespace-heavy`, `no-whitespace` (hard-cut path), `all-surrogate-pairs`, `leading-whitespace`, `trailing-whitespace`, `separators-only`, `sparse-sentences`, `repeated-runs`.
- **35 scenarios** across all three chunkers, added by appending one entry to `BuildScenarios()`:
  - `FixedSizeChunker` — 11 size/overlap pairs including `ChunkSize` 1, 2 and 3 where scalar/UTF-16 confusion shows up, and overlaps of 0, 1 and `size - 1`.
  - `RecursiveChunker`, character mode — 9 default-ladder configurations plus 2 custom ladders, all terminating in the empty separator, where the size bound is strict.
  - `RecursiveChunker`, ladders **without** the terminal empty separator — 4 configurations. Documented to leave an unsplittable atomic unit oversized, so invariants 1, 2 and 4 are asserted and 3 is not.
  - `RecursiveChunker`, token mode — 7 configurations against `TiktokenCounter` only (local, synchronous; no network call per candidate boundary).
  - `SemanticChunker` — 6 configurations across both threshold strategies, driven by `FakeEmbeddingClient` with hand-built unit vectors walking a circle (small drifts punctuated by deterministic jumps), so boundary placement is reproducible and no provider is called.
- **`CharacterModeChunkersAgreeOnTheScalarSizingUnit`** — on documents containing none of the ladder separators, `RecursiveChunker`'s hard cut must produce byte-identical boundaries to `FixedSizeChunker`. Pins the documented "an emoji counts as one scalar" claim across both implementations.

### Fixed: `SemanticChunker` full-document coverage and size backstop

- `EmittedStart` anchors the first chunk at offset 0; `EmittedEnd` extends the last chunk to `document.Text.Length`. Both `MakeChunk` and the boundary-placement logic go through them, so the backstop measures exactly what is emitted.
- `ValidateAllSentenceSizes` now measures the **emitted span** of a single-sentence chunk — the sentence plus the separator text it carries — instead of the sentence alone. A document whose sentence chunk cannot fit `MaxChunkCharacters` now throws, as documented, instead of emitting an over-budget chunk.

### Regression tests added to `SemanticChunkerTests`

`TrailingTextAfterLastSentence_IsKeptInTheLastChunk`, `LeadingTextBeforeFirstSentence_IsKeptInTheFirstChunk`, `SentenceWhoseEmittedChunkExceedsTheBudget_Throws`.

## Verification: the suite is mutation-tested

Each historical defect class was re-introduced and the suite confirmed to catch it:

| Re-introduced defect | Caught by | Scenarios failing |
|---|---|---|
| Drop `CapOverlapToBudget` (PR #67 over-budget chunks) | Invariant 3 | 5 `Recursive_default_*` |
| Drop the post-cap `Math.Min(overlapStart, chunk.Start)` (PR #67 data loss) | Invariant 2 | 3 `Recursive_tokenMode_*` |
| `ValidateAllSentenceSizes` measures the sentence, not the emitted span (PR #65) | Invariant 3 | 3 `Semantic_*` |

The second and third mutations initially **survived**, which is why the corpus gained the `sparse-sentences` kind (short sentences separated by whitespace runs of up to 200 characters) and `repeated-runs` (long character runs whose sub-word token boundaries shift when an overlap prefix is prepended), and why the token-mode sweep widened from 3 to 7 configurations.

## Design decisions

1. **Deterministic generator over FsCheck** — per the issue. The project is NUnit + NSubstitute; a shrinking property framework would be a new dependency, and a fixed-seed corpus reproduces exactly with no shrink step.
2. **Hand-rolled xorshift32 over `System.Random`** — guarantees an identical corpus on both target frameworks and across runtime versions.
3. **Size measured in each chunker's own documented unit** — Unicode scalars for the character-mode chunkers, `TiktokenCounter` tokens for token mode, UTF-16 code units for `SemanticChunker`'s `MaxChunkCharacters`.
4. **The size bound is not asserted for ladders without the terminal empty separator** — `RecursiveChunkerOptions` documents that configuration as emitting an unsplittable atomic unit oversized. Those scenarios still assert coverage, verbatim text and offset sanity.
5. **Throwing counts as honouring the size bound** — `SemanticChunker` refusing a document it cannot chunk within budget is a documented mode; the suite treats it as a skip and asserts that no scenario skipped *every* document.

## Observation, not fixed here

`RecursiveChunker.MeasureSizeAsync` returns `end - start` in character mode — UTF-16 code units, while the ladder and hard cut both work in Unicode scalars. The over-measurement is conservative: it can only trigger *extra* recursion, and the hard cut re-measures in scalars, so emitted chunks always respect the documented scalar budget. `CharacterModeChunkersAgreeOnTheScalarSizingUnit` confirms the two implementations agree on real non-BMP input. Left alone — changing it would move chunk boundaries for existing non-BMP corpora for no correctness gain.

## Explicitly out of scope

Performance/throughput assertions (flaky in CI), token-mode sweeps against a remote `ITokenCounter`, and reranker/packer/embedding invariants. `ContextPacker` has its own budget guarantees worth testing this way; a follow-up issue can take that on.
