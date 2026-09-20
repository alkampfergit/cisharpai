# RAG ingestion foundations

## Summary
Add Cisharpai.Rag so applications can configure document chunking and bulk embeddings through existing provider clients. Inputs and results stream in bounded batches while retaining source identity and explicit failure information.

Closes #28

## Design artifacts
- [Specification](spec.md)
- [Plan](plan.md)
- [Tasks](tasks.md)
- [Delegated design decisions](research.md)
- [Public API contract](contracts/api.md)

The session user requested the full cycle and delegated questions to subagents. Agent findings are recorded as delegated decisions, not human GitHub approvals.

## What's New
- New publishable Cisharpai.Rag project targeting .NET8 and .NET10, integrated with solution, tests and package build.
- Lazy fixed-size Unicode scalar chunking with configurable overlap and UTF-16 source offsets.
- Sequential bulk embedding over synchronous or asynchronous chunk streams, validated options and responses, cancellation, disposal, and failed-batch results.
- Document ingestion pipeline, DI registration, host configuration binding and keyed/scoped provider selection.
- Consumer guide and package README; wiki, release notes, project overview and embedded library skill updated.
- CI pull-request trigger includes rag101, this PR's integration base.

## Testing
- `dotnet test src/Cisharpai.Tests/Cisharpai.Tests.csproj --no-restore -c Release -m:1 -nr:false`: **826 passed on net8.0; 826 passed on net10.0; zero failures/skips**. Includes **60 RAG tests** per framework.
- `dotnet pack src/Cisharpai.Rag/Cisharpai.Rag.csproj --no-restore --no-build -c Release -m:1 -nr:false`: passed. Inspected package contents: both target framework assemblies and package README present. Local package version is only a validation default; no NuGet publication performed.
- Independent agent review found consumer-token cancellation through empty synchronous documents, missing pre-chunked input validation, and a deferred JsonDocument example lifetime issue. Fixed each; regression tests cover cancellation/input checks and host configuration binding.
- `git -c core.whitespace=cr-at-eol diff --check`: passed.
- Existing NUnit2009 warnings remain in ToolChoiceTests; no new compiler/test failures.
- GitHub CI and SonarCloud status will be recorded on PR #29 after publication.

## Notes
Ingestion foundations only: no vector storage/retrieval, file parsing, tokenizer, generation, asynchronous provider batch jobs, or ingestion-level retry policy. One full document and one batch are buffered; callers control provider token/request limits. Float vectors only. No external-service integration tests, merge, release or package publication requested.
