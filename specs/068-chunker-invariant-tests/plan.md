# Plan: Property-based invariant tests for all ITextChunker implementations

**Issue**: #68 | **Spec**: [spec.md](spec.md) | **Branch**: `feature/068-chunker-invariant-tests`

## Approach

Tests first, production change only where a test proves a defect. The suite is a single fixture with a
scenario table so a future chunker joins by appending one entry, never by copying the file.

```
ChunkerInvariantPropertyTests
├── Corpus()            → 14 document kinds × 5 fixed seeds = 70 documents
├── BuildScenarios()    → 35 (chunker, option set) pairs
├── EveryChunkSatisfiesTheChunkerInvariants(scenario)   [TestCaseSource]
│     └── for each document → AssertInvariants (4 invariants)
└── CharacterModeChunkersAgreeOnTheScalarSizingUnit()   [Values]
```

`ChunkerScenario` carries `Factory` (document → chunker, so `SemanticChunker` can get per-document fake
vectors), `Measure` (span → size in that chunker's own unit), `MaxSize`, and `SizeBoundIsStrict`.

## Steps

1. **Read the three chunkers** and establish, by reading the code, whether invariant 2 holds for each —
   the issue's open question. Record the answer in the spec before writing assertions, so the test
   encodes an intended rule rather than whatever the code happens to do.
2. **Build the deterministic generator** — xorshift32, fixed seed list, document kinds spanning ASCII,
   emoji, CJK, mixed scripts, whitespace-heavy, whitespace-free, surrogate-only, empty and sub-chunk.
3. **Build the scenario table** and the four invariant assertions, with failure messages that carry the
   seed, kind, options and escaped source text.
4. **Run.** Triage every failure as either a test bug or a production defect.
5. **Fix the production defects** the suite finds, and add example-based regression tests alongside the
   property suite so the fix is self-documenting.
6. **Mutation-test the suite** by re-introducing each of the three historical defects and confirming it
   fails. Widen the corpus until every mutation is caught — a suite that does not catch the bugs that
   motivated it has not earned its place.
7. **Docs**: `wiki/rag.md` coverage guarantees, `RELEASE_NOTES.md`, `memories/project_overview.md`.

## Risks

| Risk | Mitigation |
|---|---|
| A generated corpus that never reaches the interesting paths — a green suite that proves nothing | Mutation-test against all three historical defects; add `sparse-sentences` and `repeated-runs` and widen the token sweep until each mutation fails |
| Weakening an invariant to make a test pass | The issue forbids it. `SemanticChunker`'s gaps are fixed in production; the only permitted zero-chunk case is cross-checked against the splitter rather than accepted blindly |
| Non-reproducible failures | Fixed seeds, hand-rolled PRNG, no wall-clock or environment input; failure messages print an ASCII-safe C# literal of the source text |
| Token-mode sweeps turning into network calls | `TiktokenCounter` only, per the issue |
| The `SemanticChunker` fix changing behaviour for existing users | Full suite (1418 tests) green on both target frameworks; the behaviour change is strictly "text that used to be dropped is now kept" |

## Verification

- `dotnet test src/Cisharpai.Tests/Cisharpai.Tests.csproj` — both `net8.0` and `net10.0`.
- Mutation results recorded in [spec.md](spec.md).
