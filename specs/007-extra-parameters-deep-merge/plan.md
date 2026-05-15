# Implementation Plan: ExtraParameters & Deep Merge

**Branch**: `007-extra-parameters-deep-merge` | **Date**: 2026-05-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/007-extra-parameters-deep-merge/spec.md`

## Summary

The ExtraParameters feature provides an "escape hatch" for injecting arbitrary JSON into outbound API requests via deep merge. Combined with `IncludeRawResponse` for raw JSON capture on responses, it enables developers to use bleeding-edge provider features and debug wire-level payloads without waiting for library updates.

## Technical Context

**Language/Version**: C# on .NET 8.0 / .NET 10 (multi-target)

**Primary Dependencies**: `System.Text.Json` (runtime built-in)

**Storage**: N/A

**Testing**: NUnit — 19 tests for JsonDeepMerge, 22 tests across 5 provider ExtraParameters test classes

**Target Platform**: .NET 8.0 and .NET 10

**Project Type**: Library (cross-cutting infrastructure)

**Performance Goals**: Zero overhead when `ExtraParameters` is null (skip merge entirely)

**Constraints**: Merge must be allocation-efficient for the common case (null ExtraParameters); `Utf8JsonWriter` used for merge output

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Unified Abstraction | ✅ Pass | `ExtraParameters` available on all request types; merge happens at `LlmHttpClient` layer, provider-agnostic |
| II. No Exceptions for API Errors | ✅ Pass | `ArgumentException` only for programming errors (non-object JSON); API errors handled by response pattern |
| III. Debuggability First | ✅ Pass | This IS the debuggability feature — `RawRequestJson`, `RawResponseJson`, `ExtraParameters` |
| IV. Immutability | ✅ Pass | `ExtraParameters` is a `JsonElement?` on immutable record; `JsonDeepMerge` is stateless |
| V. Test-Driven Quality | ✅ Pass | 41 total tests covering merge semantics and all providers |
| VI. Multi-Target Compatibility | ✅ Pass | `System.Text.Json` available on both net8.0 and net10.0 |
| VII. Documentation as Deliverable | ✅ Pass | Documented in wiki/getting-started.md, wiki/runtime-configuration.md |

## Project Structure

### Documentation (this feature)

```text
specs/007-extra-parameters-deep-merge/
├── spec.md
├── plan.md
├── data-model.md
├── research.md
├── quickstart.md
├── contracts/
│   ├── JsonDeepMerge.md
│   └── LlmHttpClient-SerializeAndMerge.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
src/Cisharpai/
├── JsonDeepMerge.cs                     # Static deep-merge utility
├── LlmHttpClient.cs                     # SerializeAndMerge(), PostAsync(), PostWithRawAsync(), PostStreamAsync()
└── Models/
    ├── ChatCompletionRequest.cs          # ExtraParameters, IncludeRawResponse properties
    ├── ChatCompletionResponse.cs         # RawResponseJson, RawRequestJson properties
    ├── EmbeddingRequest.cs               # ExtraParameters, IncludeRawResponse properties
    └── EmbeddingResponse.cs              # RawResponseJson, RawRequestJson properties

src/Cisharpai.Tests/
├── Core/
│   ├── JsonDeepMergeTests.cs             # 19 tests
│   └── LlmHttpClientTests.cs            # 2 ExtraParameters tests
├── OpenAi/
│   └── OpenAiExtraParametersTests.cs     # 7 tests
├── Azure/
│   ├── AzureOpenAi/
│   │   └── AzureOpenAiExtraParametersTests.cs  # 8 tests
│   └── AzureAiInference/
│       └── AzureAiInferenceChatCompletionClientTests.cs  # 2 ExtraParameters tests
└── Anthropic/
    └── AnthropicExtraParametersTests.cs  # 3 tests
```

**Structure Decision**: The feature is cross-cutting infrastructure, not a standalone project. `JsonDeepMerge` is a static utility in the core `Cisharpai` namespace. The merge integration point is `LlmHttpClient.SerializeAndMerge()`, which all providers use. Provider-specific tests verify end-to-end behavior.

## Complexity Tracking

No violations. The feature adds 1 utility class (~75 lines) and 2 optional properties to existing request/response records.
