# Feature Specification: RAG ingestion foundations

**Feature Branch**: `010-rag-foundations`
**Created**: 2026-09-05
**Status**: Reviewed design; implementation authorized by session user
**Issue**: https://github.com/alkampfergit/cisharpai/issues/28
**Input**: Create a new RAG project with tests, starting with basic fixed-size chunking and bulk embedding; concentrate on configuration and general usage.

## User Scenarios & Testing

### User Story 1 - Configure and chunk documents (Priority: P1)
As a library consumer, I can split identified text documents into predictable overlapping chunks without losing source text or position.
**Why this priority**: Chunking is the independently usable foundation of ingestion.
**Independent Test**: Chunk known inputs and reconstruct their exact source ranges.
**Acceptance Scenarios**:
1. Given text longer than the configured size, chunking yields ordered chunks with exact source positions and the configured overlap.
2. Given Unicode supplementary characters, chunking never splits a valid surrogate pair.
3. Given empty text, no chunks are produced; whitespace is preserved.
4. Given invalid size or overlap, configuration fails before processing.

### User Story 2 - Embed large streams (Priority: P1)
As a consumer, I can embed a lazy stream of chunks without loading the entire corpus, and handle each batch as it completes.
**Why this priority**: Large ingestion jobs need bounded buffering and explicit partial results.
**Independent Test**: Use a fake embedding provider to verify request sizes, ordering, failures, and backpressure.
**Acceptance Scenarios**:
1. Given 10,000 chunks, every chunk maps to its corresponding vector, in order, and no request exceeds the configured batch size.
2. Given a failed provider batch, that batch is reported without vectors and processing stops; prior successful batches remain available.
3. Given malformed successful output, no incorrect chunk/vector associations are emitted.
4. Given cancellation or early consumer disposal, processing stops and the input enumerator is disposed.

### User Story 3 - Configure and ingest through one entry point (Priority: P2)
As a consumer, I can configure chunking, batching and provider selection once, then ingest a document stream with a small, documented API.
**Why this priority**: Configuration and ordinary application usage are the focus of this increment.
**Independent Test**: Configure services and run an in-memory document stream through a fake provider.
**Acceptance Scenarios**:
1. Direct construction and dependency injection produce equivalent output.
2. Selecting a keyed embedding provider changes provider selection without changing ingestion code.
3. Batches can span document boundaries while retaining each document ID and chunk index.
4. Documented configuration examples demonstrate defaults, overrides, errors and cancellation.

### Edge Cases
Empty input, exact-size text, final short chunks, overlap size minus one, whitespace, supplementary Unicode characters, invalid options, provider errors, mismatched vector counts, empty/non-finite/inconsistent vectors, cancellation swallowed by a provider, and early enumeration disposal.

## Requirements

### Functional Requirements
- **FR-001**: Provide a separately consumable RAG library compatible with the existing library targets.
- **FR-002**: Chunk size and overlap count Unicode scalar values; positions use source UTF-16 offsets. Default size 1024, overlap 128. Preserve text exactly and avoid redundant trailing overlap-only chunks.
- **FR-003**: Preserve document ID and zero-based chunk index. The caller owns document ID uniqueness.
- **FR-004**: Embed through the existing provider abstraction in sequential batches, default 32 chunks. Buffer at most one batch plus the current document; do not pre-enumerate the corpus.
- **FR-005**: Expose model, document input type, dimensions, raw payload capture and provider extra parameters; use float embedding output.
- **FR-006**: Yield ordered batch results with chunks, vector associations, original response metadata and success/error status. Stop after the first failed or malformed batch.
- **FR-007**: Reject invalid configuration; propagate cancellation and network/configuration exceptions. Do not add ingestion-level retries.
- **FR-008**: Support direct usage, dependency injection, host configuration binding and keyed provider selection. Snapshot options at construction.
- **FR-009**: Provide a document-to-embedding pipeline that composes independently usable chunker and bulk processor.
- **FR-010**: Include automated tests, packaging integration and current usage documentation.

### Key Entities
- Document: caller-provided identity and source text.
- Chunk: document identity, chunk index, source offset and exact text slice.
- Embedded chunk: chunk plus corresponding float vector.
- Batch result: batch index, submitted chunks, associations and provider response.

## Success Criteria
- **SC-001**: All chunk outputs match exact source substrings and expected overlap, including Unicode cases.
- **SC-002**: A 10,000-chunk test produces all expected vectors without consuming more than one batch ahead of the consumer.
- **SC-003**: All new behavior tests and the existing unit suite pass on both supported frameworks.
- **SC-004**: Usage examples cover direct chunking, bulk embedding, configured ingestion and keyed selection without external API calls in tests.

## Assumptions
- This increment covers ingestion foundations; vector stores, retrieval, document parsing, tokenizers and generation are future work.
- Size is not a provider token limit. Consumers tune size and batch size to provider limits.
- Sequential requests provide bounded resource use; parallel embedding is deferred.
- Tests belong in the existing single test project per repository policy.
- The session user explicitly delegates questions to subagents for this full cycle. Agent design reviews are recorded as delegated decisions, never misrepresented as human GitHub approvals. No merge is authorized.

## Clarifications
- Q: What does massive embedding mean? → A: Stream chunks through bounded synchronous provider request batches, not provider-specific asynchronous batch jobs. Resolved by rag_design subagent.
- Q: What are fixed-size units? → A: Unicode scalar values, with UTF-16 source positions. Resolved by rag_design subagent.
- Q: How are failures handled? → A: Yield one failed batch, preserve prior successes and raw provider response, then stop. Resolved by rag_design subagent.
- Q: How is configuration exposed? → A: Validated snapshotted options, direct constructors and DI callback with an optional provider factory. Resolved by rag_design subagent.
