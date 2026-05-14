# Implementation Plan: Testing Package

**Branch**: `006-testing-package` | **Date**: 2026-05-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/006-testing-package/spec.md`

## Summary

The `Cisharpai.Testing` package provides lightweight fake implementations of `IChatCompletionClient` and `IEmbeddingClient` that support canned response queuing, default responses, request capture, selective feature opt-out via flags enums, static response factories, and DI registration extensions. It enables unit testing of Cisharpai-dependent code without network access, API keys, or cost.

## Technical Context

**Language/Version**: C# on .NET 8.0 / .NET 10 (multi-target)

**Primary Dependencies**: `Cisharpai` (core abstractions), `Microsoft.Extensions.DependencyInjection.Abstractions`

**Storage**: N/A (in-memory only)

**Testing**: NUnit with 53 tests across 4 test classes

**Target Platform**: .NET 8.0 and .NET 10

**Project Type**: Library (NuGet package)

**Performance Goals**: N/A (test-time only)

**Constraints**: Zero external dependencies beyond DI abstractions; no HTTP, no serialization overhead

**Scale/Scope**: Covers all 6 feature interfaces across 2 client types

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Unified Abstraction | ✅ Pass | Fakes implement the same interfaces as real providers |
| II. No Exceptions for API Errors | ✅ Pass | Error responses via `FakeResponses.ChatError()` / `EmbeddingError()` |
| III. Debuggability First | ✅ Pass | `FakeResponses` factory allows custom raw response construction |
| IV. Immutability | ✅ Pass | All response DTOs are immutable records; fake state (queues, capture) is mutable by design |
| V. Test-Driven Quality | ✅ Pass | 53 unit tests across 4 test classes covering all public APIs |
| VI. Multi-Target Compatibility | ✅ Pass | `Cisharpai.Testing.csproj` targets `net8.0;net10.0` |
| VII. Documentation as Deliverable | ✅ Pass | `wiki/testing.md` comprehensive guide with examples |

## Project Structure

### Documentation (this feature)

```text
specs/006-testing-package/
├── spec.md
├── plan.md
├── data-model.md
├── research.md
├── quickstart.md
├── contracts/
│   ├── FakeChatCompletionClient.md
│   ├── FakeEmbeddingClient.md
│   ├── FakeResponses.md
│   └── FakeServiceCollectionExtensions.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
src/Cisharpai.Testing/
├── Cisharpai.Testing.csproj
├── FakeChatCompletionClient.cs       # IChatCompletionClient + 4 feature interfaces
├── FakeEmbeddingClient.cs            # IEmbeddingClient + 2 feature interfaces
├── FakeResponses.cs                  # Static factory methods (Chat, ChatError, ToolCall, etc.)
├── FakeServiceCollectionExtensions.cs # AddFakeChatCompletionClient, AddFakeEmbeddingClient
├── FakeChatFeatures.cs               # [Flags] enum: None, Streaming, ToolCalling, JsonOutput, GroundedChat, All
└── FakeEmbeddingFeatures.cs          # [Flags] enum: None, ImageEmbedding, MultimodalEmbedding, All

src/Cisharpai.Tests/Testing/
├── FakeChatCompletionClientTests.cs   # 16 tests
├── FakeEmbeddingClientTests.cs        # 12 tests
├── FakeResponsesTests.cs             # 15 tests
└── FakeServiceCollectionExtensionsTests.cs # 6 tests

wiki/
└── testing.md                         # Comprehensive usage guide
```

**Structure Decision**: Single project with no sub-namespaces. All 6 source files are in the root `Cisharpai.Testing` namespace. The project references only `Cisharpai.csproj` (for interfaces and models) and `Microsoft.Extensions.DependencyInjection.Abstractions` (for DI extensions).

## Complexity Tracking

No violations. The testing package is a single project with 6 files, well within constitution limits.
