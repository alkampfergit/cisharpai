# Implementation Plan: Embeddings (Text, Image, Multimodal)

**Branch**: `feature/gh-specify` | **Date**: 2026-05-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/004-embeddings/spec.md`

**Note**: Reverse-engineered from existing implementation.

## Summary

Provide a unified embedding subsystem across four providers (OpenAI, Azure OpenAI, Azure AI Inference, Cohere) with a shared `IEmbeddingClient` interface for text, optional `IImageEmbeddingFeature` for single-image embedding, and optional `IMultimodalEmbeddingFeature` for mixed text+image embedding. All operations return unified `EmbeddingResponse` records with no-exception error handling and optional raw JSON capture.

## Technical Context

**Language/Version**: C# on .NET 8.0 / .NET 10 (multi-target)

**Primary Dependencies**: `System.Text.Json`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.DependencyInjection`, `Azure.Identity` (Azure providers only)

**Storage**: N/A (stateless HTTP client library)

**Testing**: NUnit with NSubstitute for mocking; `MockHttpMessageHandler` for HTTP interception in unit tests

**Target Platform**: Any .NET 8.0+ runtime (library)

**Project Type**: NuGet library (6 packages)

**Performance Goals**: N/A (thin HTTP client wrapper — latency is provider-bound)

**Constraints**: No provider SDKs; all serialization via `System.Text.Json`; immutable record DTOs

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Unified Abstraction | ✅ Pass | `IEmbeddingClient` shared across 4 providers; optional features via Feature Collection |
| II. No Exceptions for API Errors | ✅ Pass | All clients catch `LlmHttpRequestException` and return `EmbeddingResponse.Error()` |
| III. Debuggability First | ✅ Pass | `RawResponseJson`/`RawRequestJson` via `IncludeRawResponse`; `ExtraParameters` deep-merge |
| IV. Immutability | ✅ Pass | `EmbeddingRequest`, `EmbeddingResponse`, `MultimodalEmbeddingInput` are all immutable records |
| V. Test-Driven Quality | ✅ Pass | 6 unit test classes, `FakeEmbeddingClient` with all 3 modes |
| VI. Multi-Target Compatibility | ✅ Pass | All projects target net8.0;net10 |
| VII. Documentation as Deliverable | ✅ Pass | `wiki/embeddings.md` covers all providers and modes |

## Project Structure

### Documentation (this feature)

```text
specs/004-embeddings/
├── spec.md
├── plan.md
├── data-model.md
├── research.md
├── quickstart.md
├── contracts/
│   ├── IEmbeddingClient.md
│   ├── IImageEmbeddingFeature.md
│   └── IMultimodalEmbeddingFeature.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
src/Cisharpai/
├── IEmbeddingClient.cs                              # Core interface
├── Models/
│   ├── EmbeddingRequest.cs                          # Unified request record
│   ├── EmbeddingResponse.cs                         # Unified response record
│   ├── EmbeddingInputType.cs                        # Input type enum
│   └── MultimodalEmbeddingInput.cs                  # Multimodal content parts
├── Features/Embeddings/
│   ├── IImageEmbeddingFeature.cs                    # Single-image feature
│   └── IMultimodalEmbeddingFeature.cs               # Multimodal feature
└── Helpers/
    └── EmbeddingHelper.cs                           # Shared response mapping

src/Cisharpai.OpenAi/
├── OpenAiEmbeddingClient.cs                         # Text embeddings only
└── Models/
    ├── OpenAiEmbeddingRequest.cs
    └── OpenAiEmbeddingResponse.cs

src/Cisharpai.Azure/
├── AzureOpenAi/
│   ├── AzureOpenAiEmbeddingClient.cs                # Text embeddings only
│   └── Models/
│       ├── AzureOpenAiEmbeddingRequest.cs
│       └── AzureOpenAiEmbeddingResponse.cs
└── AzureAiInference/
    ├── AzureAiInferenceEmbeddingClient.cs           # Text + Image embeddings
    └── Models/
        ├── AzureAiInferenceEmbeddingRequest.cs
        ├── AzureAiInferenceEmbeddingResponse.cs
        └── AzureAiInferenceImageEmbeddingRequest.cs

src/Cisharpai.Cohere/
├── CohereEmbeddingClient.cs                         # Text + Image + Multimodal
└── Models/
    ├── CohereEmbedRequest.cs
    ├── CohereEmbedResponse.cs
    └── CohereEmbedInput.cs                          # Multimodal content parts

src/Cisharpai.Testing/
├── FakeEmbeddingClient.cs                           # Fake with queues/defaults/capture
└── FakeEmbeddingFeatures.cs                         # Feature flags enum

src/Cisharpai.Tests/
├── OpenAi/OpenAiEmbeddingClientTests.cs
├── Azure/AzureAiInference/
│   ├── AzureAiInferenceEmbeddingClientTests.cs
│   └── AzureAiInferenceImageEmbeddingTests.cs
└── Cohere/
    ├── CohereEmbeddingClientTests.cs
    ├── CohereImageEmbeddingTests.cs
    └── CohereMultimodalEmbeddingTests.cs
```

**Structure Decision**: The embedding feature follows the same per-provider project layout as chat completions. Core abstractions and unified DTOs live in `Cisharpai`, provider-specific serialization models and client implementations live in their respective provider projects.

## Complexity Tracking

No constitution violations. The feature stays within the existing project structure and does not add new projects or layers.
