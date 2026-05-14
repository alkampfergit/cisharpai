# Implementation Plan: Multimodal Images & JSON Output

**Branch**: `feature/gh-specify` | **Date**: 2026-05-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-multimodal-json-output/spec.md`

## Summary

Extend the base `IChatCompletionClient` infrastructure with two complementary capabilities: (1) multimodal message content parts that allow mixing text and images in chat messages, with automatic format conversion per provider, and (2) an `IJsonOutputFeature` that enforces JSON output via JSON Mode or Structured Outputs with schema enforcement. Both features follow the library's established patterns: Feature Collection discovery, sealed record DTOs, shared helpers for cross-provider mapping, and validation before API calls.

## Technical Context

**Language/Version**: C# on .NET 8.0 and .NET 10 (multi-target)

**Primary Dependencies**:
- `System.Text.Json` — JSON Schema validation, response format wire DTOs
- `System.IO` — Image file reading for `ImageFileContentPart`
- `Microsoft.Extensions.Http` — `IHttpClientFactory`, `IHttpMessageHandlerFactory`
- `Microsoft.Extensions.DependencyInjection` — DI with keyed services
- `Azure.Core` / `Azure.Identity` — Azure AD authentication (Azure providers only)

**Storage**: N/A (stateless HTTP client library; images read from filesystem at request time)

**Testing**: NUnit + NSubstitute (unit tests), NUnit (integration tests against real APIs)

**Target Platform**: .NET 8.0 / .NET 10 server-side applications

**Project Type**: NuGet library (builds on the 6 packages from spec 001)

**Performance Goals**: Minimal overhead. Image file reads use `File.ReadAllBytesAsync` with cancellation. No unnecessary base64 re-encoding when data is already in base64 format.

**Constraints**: No provider SDKs. Immutable records for all DTOs. No exceptions for API errors. Cohere chat does not support image input — graceful degradation required.

**Scale/Scope**: 5 providers, 2 feature areas (vision + JSON output), ~43 files touched.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Unified Abstraction | **PASS** | `IJsonOutputFeature` via Feature Collection; `MessageContentPart` hierarchy works across all providers; Cohere degrades gracefully |
| II. No Exceptions for API Errors | **PASS** | `ChatCompletionResponse.Error(...)` for API failures; `ArgumentException` only for config errors (missing schema) |
| III. Debuggability First | **PASS** | JSON output requests inherit `IncludeRawResponse`/`ExtraParameters` from base request |
| IV. Immutability | **PASS** | `JsonOutputOptions`, `JsonOutputMode`, `MessageContentPart` hierarchy, `LlmMessage` are all sealed records |
| V. Test-Driven Quality | **PASS** | Unit tests for all 5 providers (JSON output + vision) + model validation; integration tests for JSON output |
| VI. Multi-Target Compatibility | **PASS** | All projects target net8.0;net10.0 |
| VII. Documentation as Deliverable | **PASS** | `wiki/json-output.md` and `wiki/vision.md` complete with examples, provider matrices, and troubleshooting |

## Project Structure

### Documentation (this feature)

```text
specs/003-multimodal-json-output/
├── spec.md
├── plan.md
├── data-model.md
├── research.md
├── quickstart.md
├── contracts/
│   ├── IJsonOutputFeature.md
│   └── multimodal-messages.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
src/
├── Cisharpai/                                 # Core abstractions
│   ├── Features/Chat/
│   │   └── IJsonOutputFeature.cs              # JSON output feature interface
│   ├── Models/
│   │   ├── JsonOutputOptions.cs               # Mode + schema name + schema + strict
│   │   ├── JsonOutputMode.cs                  # Enum: JsonMode, JsonSchema
│   │   ├── MessageContentPart.cs              # Abstract base + Text, ImageFile, ImageBase64
│   │   └── LlmMessage.cs                      # ContentParts + WithImage/WithBase64Image factories
│   ├── Helpers/
│   │   ├── JsonOutputHelper.cs                # System message injection, code fence stripping
│   │   └── ContentPartHelper.cs               # Content extraction + OpenAI-style image mapping
│   └── ImageDataUriHelper.cs                  # File → data URI, MIME detection
│
├── Cisharpai.OpenAi/
│   ├── OpenAiChatCompletionClient.cs          # IJsonOutputFeature + vision content parts
│   └── Models/
│       ├── OpenAiResponseFormat.cs            # response_format + text.format (Responses API)
│       └── OpenAiContentPart.cs               # OpenAiContentPart, OpenAiImageUrl
│
├── Cisharpai.Azure/
│   ├── AzureOpenAi/
│   │   ├── AzureOpenAiChatCompletionClient.cs # IJsonOutputFeature + vision
│   │   └── Models/
│   │       ├── AzureOpenAiResponseFormat.cs   # response_format wire DTO
│   │       └── AzureOpenAiContentPart.cs      # Content part + image URL wire DTOs
│   ├── AzureAiInference/
│   │   ├── AzureAiInferenceChatCompletionClient.cs # IJsonOutputFeature + vision
│   │   └── Models/
│   │       ├── AzureAiInferenceResponseFormat.cs   # response_format wire DTO
│   │       └── AzureAiInferenceContentPart.cs      # Content part + image URL wire DTOs
│
├── Cisharpai.Anthropic/
│   ├── AnthropicChatCompletionClient.cs       # IJsonOutputFeature + vision (base64 source)
│   └── Models/
│       ├── AnthropicOutputConfig.cs            # output_config.format wire DTO
│       └── AnthropicImageSource.cs             # base64 image source wire DTO
│
├── Cisharpai.Cohere/
│   ├── CohereChatCompletionClient.cs          # IJsonOutputFeature + text-only fallback for vision
│   └── Models/
│       └── CohereChatResponseFormat.cs         # response_format wire DTO
│
├── Cisharpai.Testing/
│   └── FakeChatCompletionClient.cs            # IJsonOutputFeature: queue, default, capture
│
├── Cisharpai.Tests/
│   ├── Models/JsonOutputOptionsTests.cs
│   ├── Models/MessageContentPartTests.cs
│   ├── OpenAi/OpenAiJsonOutputTests.cs
│   ├── OpenAi/OpenAiVisionTests.cs
│   ├── Azure/AzureOpenAi/AzureOpenAiJsonOutputTests.cs
│   ├── Azure/AzureOpenAi/AzureOpenAiVisionTests.cs
│   ├── Azure/AzureAiInference/AzureAiInferenceJsonOutputTests.cs
│   ├── Azure/AzureAiInference/AzureAiInferenceVisionTests.cs
│   ├── Anthropic/AnthropicJsonOutputTests.cs
│   ├── Anthropic/AnthropicVisionTests.cs
│   ├── Cohere/CohereJsonOutputTests.cs
│   ├── Cohere/CohereVisionTests.cs
│   ├── Cohere/ImageDataUriHelperTests.cs
│   └── Testing/FakeChatCompletionClientTests.cs
│
├── Cisharpai.Integration.Tests/
│   ├── OpenAi/OpenAiJsonOutputIntegrationTests.cs
│   ├── AzureOpenAi/AzureOpenAiJsonOutputIntegrationTests.cs
│   ├── AzureAiInference/AzureAiInferenceJsonOutputIntegrationTests.cs
│   ├── Anthropic/AnthropicJsonOutputIntegrationTests.cs
│   └── Cohere/CohereJsonOutputIntegrationTests.cs
│
└── Cisharp.Console/
    └── Scenarios/OpenAiJsonOutputScenario.cs  # Interactive JSON mode + structured outputs demo
```

**Structure Decision**: Both vision (multimodal messages) and JSON output are layered onto the existing per-provider structure. Vision is integrated into the message-building path of each provider's `GetChatCompletionAsync`, while JSON output is a separate Feature Collection interface (`IJsonOutputFeature`) with its own method. Shared helpers (`JsonOutputHelper`, `ContentPartHelper`, `ImageDataUriHelper`) live in the core project.

## Complexity Tracking

No constitution violations detected. The two feature areas (vision + JSON output) share no code beyond the base `ChatCompletionRequest`, so they are cleanly separable within the same spec. The Anthropic image format difference is isolated to `AnthropicChatCompletionClient`'s message-building logic.
