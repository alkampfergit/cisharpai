# Tasks: RAG ingestion foundations

## Phase 1: Setup
- [x] T001 Resolve delegated design questions and write specification in specs/010-rag-foundations/spec.md.
- [x] T002 Record plan, contracts and research in specs/010-rag-foundations/.
- [ ] T003 Create src/Cisharpai.Rag/Cisharpai.Rag.csproj and register it in Cisharpai.sln, the test project and scripts/build.ps1.

## Phase 2: Shared contracts
- [ ] T004 Create models and option classes in src/Cisharpai.Rag/Models/, Chunking/ and Embeddings/.

## Phase 3: US1 - Fixed-size chunking
- [ ] T005 [P] [US1] Write chunk boundaries, overlap, Unicode, invalid options and lazy iteration tests in src/Cisharpai.Tests/Rag/FixedSizeChunkerTests.cs.
- [ ] T006 [US1] Implement ITextChunker and FixedSizeChunker in src/Cisharpai.Rag/Chunking/.

## Phase 4: US2 - Bulk embedding
- [ ] T007 [P] [US2] Write batching, ordering, 10,000 inputs, option forwarding, provider errors, malformed output and cancellation tests in src/Cisharpai.Tests/Rag/BulkEmbeddingProcessorTests.cs.
- [ ] T008 [US2] Implement bounded processing and validation in src/Cisharpai.Rag/Embeddings/BulkEmbeddingProcessor.cs.

## Phase 5: US3 - Configuration and general usage
- [ ] T009 [P] [US3] Write direct/DI/keyed provider, option snapshot, pipeline and disposal tests in src/Cisharpai.Tests/Rag/RagIngestionPipelineTests.cs.
- [ ] T010 [US3] Implement composition and DI options in src/Cisharpai.Rag/RagIngestionPipeline.cs and ServiceCollectionExtensions.cs.

## Phase 6: Documentation and validation
- [ ] T011 [P] Document defaults, binding, direct/DI use and failures in wiki/rag.md and src/Cisharpai.Rag/README.md; update wiki/index.md, wiki/provider-features.md, RELEASE_NOTES.md, memories/project_overview.md and llm/cisharpai-expert/SKILL.md.
- [ ] T012 Include rag101 PRs in .github/workflows/ci.yml validation.
- [ ] T013 Run unit suites on net8.0/net10.0 and pack Cisharpai.Rag; record evidence in specs/010-rag-foundations/pr-report.md.
- [ ] T014 Independently review contracts, configuration, disposal and source/vector associations; resolve findings and update specs/010-rag-foundations/pr-report.md.

## Dependencies and parallel work
T001→T002→T003→T004. After shared contracts, US1 and US2 are independent and can run in separate agents. US3 depends on both APIs. T011 can run alongside implementation after contracts stabilize. T013/T014 follow implementation, with fixes rerun as needed.

## Implementation strategy
Write tests before code per story. Deliver chunker, then bulk processor and composition. Use fake providers only. Retain the draft PR if validation is incomplete; do not merge.
