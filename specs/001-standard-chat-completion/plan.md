# Implementation Plan: Standard Chat Completion

**Branch**: `feature/gh-specify` | **Date**: 2026-05-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-standard-chat-completion/spec.md`

## Summary

Provide a unified `IChatCompletionClient` interface backed by five provider implementations (OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere), each translating the common request/response DTOs to provider-specific wire formats. The architecture uses a shared `LlmHttpClient` for HTTP transport, `System.Text.Json` for serialization with deep-merge support, and the Feature Collection pattern for optional capability discovery. DI extensions with Polly resilience and keyed registration enable production-grade consumption.

## Technical Context

**Language/Version**: C# on .NET 8.0 and .NET 10 (multi-target)

**Primary Dependencies**:
- `System.Text.Json` — serialization
- `Microsoft.Extensions.Http` — `IHttpClientFactory`, `IHttpMessageHandlerFactory`
- `Microsoft.Extensions.DependencyInjection` — DI with keyed services
- `Microsoft.Extensions.Http.Resilience` / `Polly` — retry, circuit breaker
- `Microsoft.Extensions.Logging` — structured logging
- `System.Diagnostics` — OpenTelemetry-compatible tracing
- `Azure.Core` / `Azure.Identity` — Azure AD authentication (Azure providers only)

**Storage**: N/A (stateless HTTP client library)

**Testing**: NUnit + NSubstitute (unit tests), NUnit (integration tests against real APIs)

**Target Platform**: .NET 8.0 / .NET 10 server-side applications

**Project Type**: NuGet library (6 packages: Cisharpai, Cisharpai.OpenAi, Cisharpai.Azure, Cisharpai.Anthropic, Cisharpai.Cohere, Cisharpai.Testing)

**Performance Goals**: Minimal overhead over raw HttpClient; no unnecessary allocations in hot paths; SSE stream parsing with zero-copy where possible.

**Constraints**: No provider SDKs (raw HttpClient only). Immutable records for all DTOs. No exceptions for API errors.

**Scale/Scope**: 5 providers, 1 core interface, 6 NuGet packages, ~50 source files for this feature.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Unified Abstraction | **PASS** | Single `IChatCompletionClient` interface, Feature Collection for optional capabilities |
| II. No Exceptions for API Errors | **PASS** | All providers catch `LlmHttpRequestException` and return `ChatCompletionResponse.Error(...)` |
| III. Debuggability First | **PASS** | `RawResponseJson`/`RawRequestJson` via `IncludeRawResponse`, `ExtraParameters` deep-merge |
| IV. Immutability | **PASS** | `ChatCompletionRequest` and `ChatCompletionResponse` are sealed records |
| V. Test-Driven Quality | **PASS** | Unit tests for all 5 providers + DI + fakes; integration tests for all 5 |
| VI. Multi-Target Compatibility | **PASS** | All projects target net8.0;net10.0 |
| VII. Documentation as Deliverable | **PASS** | wiki/ has getting-started, provider-features, feature-extensions pages |

## Project Structure

### Documentation (this feature)

```text
specs/001-standard-chat-completion/
├── spec.md
├── plan.md
├── data-model.md
├── research.md
├── quickstart.md
├── contracts/
│   ├── IChatCompletionClient.md
│   └── IFeatureCollection.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── Cisharpai/                          # Core abstractions package
│   ├── IChatCompletionClient.cs        # Main interface
│   ├── LlmHttpClient.cs               # Shared HTTP transport
│   ├── LlmHttpRequestException.cs     # Custom HTTP exception
│   ├── JsonDeepMerge.cs               # ExtraParameters merge engine
│   ├── CisharpaiTelemetry.cs          # OpenTelemetry ActivitySource
│   ├── HttpClientBuilderExtensions.cs  # Polly resilience
│   ├── ImageDataUriHelper.cs           # Image MIME detection
│   ├── Models/
│   │   ├── ChatCompletionRequest.cs
│   │   ├── ChatCompletionResponse.cs
│   │   ├── LlmMessage.cs
│   │   ├── LlmRole.cs
│   │   ├── MessageContentPart.cs
│   │   └── ChatCompletionChunk.cs
│   ├── Helpers/
│   │   ├── RoleMapper.cs
│   │   ├── ContentPartHelper.cs
│   │   ├── ToolCallingHelper.cs
│   │   └── JsonOutputHelper.cs
│   └── Features/
│       ├── IHasFeatures.cs
│       ├── IFeatureCollection.cs
│       ├── FeatureCollection.cs
│       └── Chat/
│           ├── IStreamingChatFeature.cs
│           ├── IJsonOutputFeature.cs
│           ├── IToolCallingFeature.cs
│           └── IGroundedChatFeature.cs
│
├── Cisharpai.OpenAi/                   # OpenAI provider package
│   ├── OpenAiChatCompletionClient.cs
│   ├── OpenAiClientOptions.cs
│   ├── OpenAiAuthenticationHandler.cs
│   ├── OpenAiServiceCollectionExtensions.cs
│   └── Models/                         # Provider-specific wire DTOs
│
├── Cisharpai.Azure/                    # Azure provider package (2 providers)
│   ├── AzureOpenAi/
│   │   ├── AzureOpenAiChatCompletionClient.cs
│   │   ├── AzureOpenAiClientOptions.cs
│   │   └── Models/
│   ├── AzureAiInference/
│   │   ├── AzureAiInferenceChatCompletionClient.cs
│   │   ├── AzureAiInferenceClientOptions.cs
│   │   └── Models/
│   ├── Common/
│   │   ├── AzureAuthenticationHandler.cs
│   │   └── AzureClientOptionsBase.cs
│   └── Extensions/
│       ├── AzureOpenAiServiceCollectionExtensions.cs
│       └── AzureAiInferenceServiceCollectionExtensions.cs
│
├── Cisharpai.Anthropic/                # Anthropic provider package
│   ├── AnthropicChatCompletionClient.cs
│   ├── AnthropicClientOptions.cs
│   ├── AnthropicAuthenticationHandler.cs
│   ├── AnthropicServiceCollectionExtensions.cs
│   └── Models/
│
├── Cisharpai.Cohere/                   # Cohere provider package
│   ├── CohereChatCompletionClient.cs
│   ├── CohereClientOptions.cs
│   ├── CohereAuthenticationHandler.cs
│   ├── CohereServiceCollectionExtensions.cs
│   └── Models/
│
├── Cisharpai.Testing/                  # Testing fakes package
│   ├── FakeChatCompletionClient.cs
│   └── FakeServiceCollectionExtensions.cs
│
├── Cisharpai.Tests/                    # Unit tests (net8.0;net10.0)
│   ├── Core/
│   ├── OpenAi/
│   ├── Azure/
│   ├── Anthropic/
│   ├── Cohere/
│   ├── Testing/
│   └── DependencyInjection/
│
└── Cisharpai.Integration.Tests/        # Integration tests (net10.0 only)
    ├── OpenAi/
    ├── AzureOpenAi/
    ├── AzureAiInference/
    ├── Anthropic/
    └── Cohere/
```

**Structure Decision**: One core abstractions project + one project per provider (Azure bundles both Azure providers). Separate Testing project for consumer-facing fakes. Single unit test project multi-targets both frameworks.

## Complexity Tracking

No constitution violations detected. The five-provider architecture stays within the "one project per concern" principle, and the Azure bundle is justified because both Azure providers share authentication infrastructure.
