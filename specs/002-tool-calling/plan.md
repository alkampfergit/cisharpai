# Implementation Plan: Tool Calling

**Branch**: `feature/gh-specify` | **Date**: 2026-05-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-tool-calling/spec.md`

## Summary

Extend the base `IChatCompletionClient` infrastructure (spec 001) with an optional `IToolCallingFeature` that lets callers define functions the model can invoke, control selection strategy, and close the tool-calling loop by sending results back. Each of the five providers maps the unified `ToolDefinition`, `ToolCall`, and `ToolChoice` types to its native wire format while the shared `ToolCallingHelper` provides common argument parsing and choice mapping logic. A `FakeChatCompletionClient` extension supports unit-testing tool workflows without real API calls.

## Technical Context

**Language/Version**: C# on .NET 8.0 and .NET 10 (multi-target)

**Primary Dependencies**:
- `System.Text.Json` — JSON Schema parameters, argument parsing, provider wire DTOs
- `Microsoft.Extensions.Http` — `IHttpClientFactory`, `IHttpMessageHandlerFactory`
- `Microsoft.Extensions.DependencyInjection` — DI with keyed services
- `Microsoft.Extensions.Http.Resilience` / `Polly` — retry, circuit breaker
- `Azure.Core` / `Azure.Identity` — Azure AD authentication (Azure providers only)

**Storage**: N/A (stateless HTTP client library)

**Testing**: NUnit + NSubstitute (unit tests), NUnit (integration tests against real APIs)

**Target Platform**: .NET 8.0 / .NET 10 server-side applications

**Project Type**: NuGet library (builds on the 6 packages from spec 001)

**Performance Goals**: Minimal overhead; JSON argument parsing uses `JsonDocument.Parse` with `.Clone()` to avoid retaining full buffers.

**Constraints**: No provider SDKs (raw HttpClient only). Immutable records for all DTOs. No exceptions for API errors. Unparseable tool call arguments are wrapped as raw JSON strings rather than throwing.

**Scale/Scope**: 5 providers, 1 feature interface, ~40 files touched (core models + helpers + 5 provider implementations + 5 provider wire DTOs + tests + wiki + console demo).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Unified Abstraction | **PASS** | Single `IToolCallingFeature` interface, discoverable via Feature Collection; all 5 providers implement it |
| II. No Exceptions for API Errors | **PASS** | `ToolCallingResponse.Error(...)` factory; providers catch `LlmHttpRequestException` |
| III. Debuggability First | **PASS** | Tool calling requests inherit `IncludeRawResponse` and `ExtraParameters` from base `ChatCompletionRequest` |
| IV. Immutability | **PASS** | `ToolDefinition`, `ToolCall`, `ToolChoice`, `ToolCallingOptions`, `ToolCallingResponse`, `ToolResult`, `ToolCallDelta` are all sealed records |
| V. Test-Driven Quality | **PASS** | Unit tests for all 5 providers' tool calling + model validation + fake client; integration tests for all 5 |
| VI. Multi-Target Compatibility | **PASS** | All projects target net8.0;net10.0 |
| VII. Documentation as Deliverable | **PASS** | `wiki/tool-calling.md` complete with quick start, multi-turn, provider matrix, and provider differences |

## Project Structure

### Documentation (this feature)

```text
specs/002-tool-calling/
├── spec.md
├── plan.md
├── data-model.md
├── research.md
├── quickstart.md
├── contracts/
│   └── IToolCallingFeature.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
src/
├── Cisharpai/                                 # Core abstractions
│   ├── Features/Chat/
│   │   └── IToolCallingFeature.cs             # Feature interface
│   ├── Models/
│   │   ├── ToolDefinition.cs                  # Tool definition with JSON Schema params
│   │   ├── ToolCall.cs                        # Model's tool invocation request
│   │   ├── ToolChoice.cs                      # Selection strategy (Auto/None/Required/Specific)
│   │   ├── ToolCallingOptions.cs              # Options: tool list + choice
│   │   ├── ToolCallingResponse.cs             # Response wrapping base + tool calls
│   │   ├── ToolResult.cs                      # Tool execution output
│   │   ├── ChatCompletionChunk.cs             # Includes ToolCallDelta for streaming
│   │   └── LlmMessage.cs                      # Extended with ToolCallId + ToolCalls
│   └── Helpers/
│       └── ToolCallingHelper.cs               # Shared: MapResponseToolCalls, MapToolChoice, MapStreamToolCallDelta
│
├── Cisharpai.OpenAi/
│   ├── OpenAiChatCompletionClient.cs          # IToolCallingFeature implementation
│   └── Models/
│       ├── OpenAiToolDefinition.cs            # Wire DTOs: OpenAiToolDefinition, OpenAiToolCall, etc.
│       ├── OpenAiChatRequest.cs               # tools + tool_choice fields
│       └── OpenAiReasoningRequest.cs          # tools + tool_choice (max_completion_tokens)
│
├── Cisharpai.Azure/
│   ├── AzureOpenAi/
│   │   ├── AzureOpenAiChatCompletionClient.cs # IToolCallingFeature implementation
│   │   └── Models/
│   │       ├── AzureOpenAiToolDefinition.cs   # Wire DTOs
│   │       └── AzureOpenAiChatRequest.cs      # tools + tool_choice fields
│   ├── AzureAiInference/
│   │   ├── AzureAiInferenceChatCompletionClient.cs # IToolCallingFeature implementation
│   │   └── Models/
│   │       ├── AzureAiInferenceToolDefinition.cs   # Wire DTOs
│   │       └── AzureAiInferenceChatRequest.cs      # tools + tool_choice fields
│
├── Cisharpai.Anthropic/
│   ├── AnthropicChatCompletionClient.cs       # IToolCallingFeature implementation
│   └── Models/
│       ├── AnthropicToolDefinition.cs         # Wire DTOs (input_schema, tool_use/tool_result)
│       └── AnthropicChatRequest.cs            # tools + tool_choice fields
│
├── Cisharpai.Cohere/
│   ├── CohereChatCompletionClient.cs          # IToolCallingFeature implementation
│   └── Models/
│       ├── CohereToolDefinition.cs            # Wire DTOs (uppercase choice strings, strict_tools)
│       └── CohereChatRequest.cs               # tools + tool_choice + strict_tools fields
│
├── Cisharpai.Testing/
│   ├── FakeChatCompletionClient.cs            # IToolCallingFeature: queue, default, capture
│   ├── FakeResponses.cs                       # ToolCall() and ToolCalls() factories
│   └── FakeChatFeatures.cs                    # FakeChatFeatures.ToolCalling flag
│
├── Cisharpai.Tests/
│   ├── OpenAi/OpenAiToolCallingTests.cs
│   ├── Azure/AzureOpenAi/AzureOpenAiToolCallingTests.cs
│   ├── Azure/AzureAiInference/AzureAiInferenceToolCallingTests.cs
│   ├── Anthropic/AnthropicToolCallingTests.cs
│   ├── Cohere/CohereToolCallingTests.cs
│   ├── Models/ToolDefinitionTests.cs
│   ├── Models/ToolCallingOptionsTests.cs
│   ├── Models/ToolCallingResponseTests.cs
│   ├── Testing/FakeChatCompletionClientTests.cs
│   └── Features/FeatureDiscoveryTests.cs
│
├── Cisharpai.Integration.Tests/
│   ├── OpenAi/OpenAiToolCallingIntegrationTests.cs
│   ├── AzureOpenAi/AzureOpenAiToolCallingIntegrationTests.cs
│   ├── AzureAiInference/AzureAiInferenceToolCallingIntegrationTests.cs
│   ├── Anthropic/AnthropicToolCallingIntegrationTests.cs
│   └── Cohere/CohereToolCallingIntegrationTests.cs
│
└── Cisharp.Console/
    └── Scenarios/ToolCallingScenario.cs       # Interactive multi-turn weather tool demo
```

**Structure Decision**: Tool calling is layered on top of the existing per-provider project structure from spec 001. Each provider adds tool-related fields to its existing chat request wire DTOs and implements `IToolCallingFeature` alongside the existing `IChatCompletionClient`. Shared mapping logic lives in `ToolCallingHelper` in the core project to avoid duplication.

## Complexity Tracking

No constitution violations detected. The `ToolCallingHelper` shared helper centralises common logic (argument parsing, choice mapping, streaming delta mapping) rather than duplicating it across five providers, which keeps each provider implementation focused on its unique wire format.
