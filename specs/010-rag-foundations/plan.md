# Implementation Plan: RAG ingestion foundations

**Branch**: `010-rag-foundations` | **Date**: 2026-09-05 | **Spec**: [spec.md](spec.md)
**Issue**: #28 | **PR base**: `rag101`

## Summary
Add Cisharpai.Rag as a provider-independent ingestion library. Compose a lazy Unicode-aware fixed-size chunker, sequential bounded bulk embedding processor, and document ingestion pipeline. Configure through simple option classes and DI callbacks, including a provider factory for keyed services.

## Technical Context
- Language/platform: C#, .NET 8.0 and .NET 10.0.
- Dependencies: existing Cisharpai core; centrally pinned Microsoft.Extensions.DependencyInjection.Abstractions and Microsoft.Extensions.Options. Host configuration binding remains the consuming application's responsibility.
- Storage: none. No network access beyond the injected embedding client.
- Testing: existing NUnit/NSubstitute unit project on both frameworks; fake providers only.
- Performance: lazy O(text length) chunking for fixed configuration; one active embedding request, one buffered batch, one current document. No corpus-sized materialization.
- Compatibility: no core API changes; preserve existing grounded-chat DocumentChunk by introducing RagDocument/TextChunk.

## Constitution Check
All seven principles satisfied: provider abstraction reused; API errors returned as results; raw responses and extra parameters retained; data records immutable in the same conventions as core; tests in the single multitarget test project; both frameworks; documentation included. No new optional core feature means existing embedding fakes can be reused unchanged. Test and SonarCloud outcomes must be recorded honestly. No external integration tests requested.

## Project Structure
- `src/Cisharpai.Rag/`: library project, Models/, Chunking/, Embeddings/, RagIngestionPipeline, DI extension and options.
- `src/Cisharpai.Tests/Rag/`: chunker, processor, pipeline and configuration tests.
- `specs/010-rag-foundations/`: specification, research, plan, model, contracts, quickstart, tasks and PR report.
- `wiki/rag.md`, existing wiki index/matrix, release notes, overview and library expert skill: consumption guidance.
- Solution and packaging script include the new library. CI must also run for PRs targeting rag101 (existing triggers omit it).

## Execution and Validation
Tests precede implementations within each task group. Run the full existing unit project on both target frameworks, pack the new project, and inspect PR CI. Independent review checks Unicode, bounded enumeration, malformed provider responses, cancellation, options snapshots, scoped provider DI lifetimes and docs/API agreement.

## Workflow adaptation
The user's latest instruction delegates questions to subagents. Record their decisions and reviews without fabricating owner approvals. Use GitHub connector for authenticated operations because installed gh lacks authentication. Add the missing PR-report template as a necessary setup artifact in this feature change. The existing initialized Spec Kit scripts remain authoritative. Do not merge or release.

## Complexity Tracking
No constitution exceptions required. Sequential batching intentionally avoids concurrency/retry policy complexity.
