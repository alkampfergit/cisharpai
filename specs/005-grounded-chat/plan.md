# Implementation Plan: Grounded Chat (RAG)

**Branch**: `feature/gh-specify` | **Date**: 2026-05-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/005-grounded-chat/spec.md`

**Note**: Reverse-engineered from existing implementation.

## Summary

Add document-grounded chat (RAG) with citations as an optional feature on the Cohere provider. The feature injects documents into chat requests, controls citation generation mode, and maps provider-specific citation responses to unified models with character offsets and source references.

## Technical Context

**Language/Version**: C# on .NET 8.0 / .NET 10 (multi-target)

**Primary Dependencies**: `System.Text.Json`, `Microsoft.Extensions.Logging`

**Storage**: N/A (stateless HTTP client library)

**Testing**: NUnit with `MockHttpMessageHandler` for HTTP interception

**Target Platform**: Any .NET 8.0+ runtime (library)

**Project Type**: NuGet library

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Unified Abstraction | ✅ Pass | `IGroundedChatFeature` via Feature Collection; unified models in `Cisharpai.Models` |
| II. No Exceptions for API Errors | ✅ Pass | All errors return `GroundedChatCompletionResponse.Error()`; validation errors caught internally |
| III. Debuggability First | ✅ Pass | `RawResponseJson`/`RawRequestJson` via `IncludeRawResponse`; `ExtraParameters` deep-merge |
| IV. Immutability | ✅ Pass | All DTOs are immutable records |
| V. Test-Driven Quality | ✅ Pass | 36 unit tests across 3 test classes; grounded chat support in `FakeChatCompletionClient` |
| VI. Multi-Target Compatibility | ✅ Pass | All projects target net8.0;net10 |
| VII. Documentation as Deliverable | ✅ Pass | `wiki/grounded-chat.md` comprehensive guide |

## Project Structure

### Documentation (this feature)

```text
specs/005-grounded-chat/
├── spec.md
├── plan.md
├── data-model.md
├── research.md
├── quickstart.md
├── contracts/
│   └── IGroundedChatFeature.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
src/Cisharpai/
├── Features/Chat/
│   └── IGroundedChatFeature.cs              # Feature interface
└── Models/
    ├── GroundedChatCompletionResponse.cs    # Response with citations
    ├── GroundedChatOptions.cs               # Options (documents + citation mode)
    ├── DocumentChunk.cs                     # Document input with validation
    ├── Citation.cs                          # Citation with offsets
    ├── CitationSource.cs                    # Source document reference
    └── CitationMode.cs                      # Accurate/Fast/Enabled enum

src/Cisharpai.Cohere/
├── CohereChatCompletionClient.cs            # GetGroundedChatCompletionAsync + helpers
└── Models/
    ├── CohereChatCitation.cs                # Provider citation models
    └── CohereChatDocument.cs                # Provider document + citation options models

src/Cisharpai.Tests/
├── Cohere/CohereGroundedChatTests.cs        # 20 tests
├── Models/DocumentChunkTests.cs             # 8 validation tests
└── Models/GroundedChatOptionsTests.cs       # 8 validation tests
```

**Structure Decision**: Grounded chat follows the established Feature Collection pattern. Core models live in `Cisharpai.Models`, the feature interface in `Cisharpai.Features.Chat`, and the provider implementation in the existing `CohereChatCompletionClient`.

## Complexity Tracking

No constitution violations. The feature adds models and one feature interface to the existing Cohere client without new projects or layers.
