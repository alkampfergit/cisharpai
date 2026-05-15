# Implementation Plan: Streaming Chat Completions

**Branch**: `feature/gh-specify` | **Date**: 2026-05-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/008-streaming-chat/spec.md`

**Note**: Reverse-engineered from existing implementation.

## Summary

Streaming chat completions delivers real-time token-by-token output via `IAsyncEnumerable<ChatCompletionChunk>`,
exposed through the `IStreamingChatFeature` optional feature interface. The shared `LlmHttpClient.PostStreamAsync`
handles SSE line parsing, while each provider deserializes its own event format into the unified chunk model.
A dedicated resilience handler removes timeout constraints for long-running streams.

## Technical Context

**Language/Version**: C# on .NET 8.0 / .NET 10 (multi-target)

**Primary Dependencies**: `System.Text.Json` (deserialization), `Microsoft.Extensions.Http.Resilience` + Polly
(resilience handlers), `System.Diagnostics` (tracing), `Microsoft.Extensions.Logging` (structured logging)

**Storage**: N/A (stateless streaming)

**Testing**: NUnit + NSubstitute. MockHttpMessageHandler for simulating SSE streams from byte arrays.

**Target Platform**: .NET class library (NuGet packages)

**Project Type**: Library — one core package + one package per provider + testing fakes package

**Performance Goals**: First chunk latency matches provider latency (no buffering). Zero allocation on
skip paths (unknown events, blank lines).

**Constraints**: `IAsyncEnumerable<T>` requires .NET Standard 2.1+ / .NET Core 3.0+, satisfied by the
.NET 8.0 minimum target. Streams must not be bounded by timeout policies.

**Scale/Scope**: 5 provider implementations, 7 stream event model files, 35+ unit tests, 3 integration
test files.

## Constitution Check

*GATE: All checks pass.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Unified Abstraction | PASS | `IStreamingChatFeature` discovered via Feature Collection; same interface for all 5 providers |
| II. No Exceptions for API Errors | PASS | HTTP errors throw `LlmHttpRequestException` before any chunks (fail-fast); this is a network-level exception, not an API error. Stream content never throws. |
| III. Debuggability First | PASS | `PostStreamAsync` accepts `ExtraParameters` for deep merge; structured logging (EventIds 1003-1005) and tracing spans for all stream requests |
| IV. Immutability | PASS | `ChatCompletionChunk` and `ToolCallDelta` are immutable C# records |
| V. Test-Driven Quality | PASS | 35+ unit tests across 5 providers; 3 integration test files; `FakeChatCompletionClient` supports streaming |
| VI. Multi-Target Compatibility | PASS | All projects target .NET 8.0 and .NET 10; `IAsyncEnumerable<T>` available on both |
| VII. Documentation as Deliverable | PASS | `wiki/streaming.md` (174 lines) fully documents the feature |

## Project Structure

### Documentation (this feature)

```text
specs/008-streaming-chat/
├── spec.md              # Business specification
├── plan.md              # This file
├── data-model.md        # Chunk and event models
├── quickstart.md        # Usage guide
├── research.md          # Technical decisions
├── contracts/           # Public API contracts
│   └── streaming-chat-feature.md
└── checklists/
    └── requirements.md  # Quality checklist
```

### Source Code (repository root)

```text
src/Cisharpai/
├── Features/Chat/IStreamingChatFeature.cs          # Feature interface
├── Models/ChatCompletionChunk.cs                    # Unified chunk + ToolCallDelta
├── LlmHttpClient.cs                                # PostStreamAsync SSE parser
└── HttpClientBuilderExtensions.cs                   # AddCisharpaiStreamingResilienceHandler

src/Cisharpai.OpenAi/
├── OpenAiChatCompletionClient.cs                    # StreamLegacyChatAsync + StreamResponsesApiAsync
└── Models/
    ├── OpenAiStreamChunk.cs                         # Legacy SSE chunk model
    └── OpenAiResponsesStreamEvent.cs                # GPT-5 Responses API event model

src/Cisharpai.Azure/
├── AzureOpenAi/
│   ├── AzureOpenAiChatCompletionClient.cs           # Legacy + Reasoning + GPT-5 streaming
│   └── Models/
│       ├── AzureOpenAiStreamChunk.cs                # Azure OpenAI chunk model
│       └── AzureOpenAiResponsesStreamEvent.cs       # Azure GPT-5 event model
└── AzureAiInference/
    ├── AzureAiInferenceChatCompletionClient.cs      # Standard + Reasoning streaming
    └── Models/
        └── AzureAiInferenceStreamChunk.cs           # Azure AI Inference chunk model

src/Cisharpai.Anthropic/
├── AnthropicChatCompletionClient.cs                 # Event-based SSE streaming
└── Models/
    └── AnthropicStreamEvent.cs                      # message_start/content_block_delta/message_delta

src/Cisharpai.Cohere/
├── CohereChatCompletionClient.cs                    # Event-based SSE streaming
└── Models/
    └── CohereStreamEvent.cs                         # stream-start/content-delta/message-end

src/Cisharpai.Testing/
├── FakeChatCompletionClient.cs                      # Queue-based streaming mock
└── FakeChatFeatures.cs                              # Streaming flag in feature flags enum

src/Cisharpai.Tests/
├── OpenAi/OpenAiStreamingTests.cs                   # 7 tests
├── Azure/AzureOpenAi/AzureOpenAiStreamingTests.cs   # 7 tests
├── Azure/AzureAiInference/AzureAiInferenceStreamingTests.cs  # 6 tests
├── Anthropic/AnthropicStreamingTests.cs             # 7 tests
└── Cohere/CohereStreamingTests.cs                   # 8 tests

src/Cisharpai.Integration.Tests/
├── OpenAi/OpenAiStreamingIntegrationTests.cs
├── Anthropic/AnthropicStreamingIntegrationTests.cs
└── Cohere/CohereStreamingIntegrationTests.cs

src/Cisharp.Console/Scenarios/
└── OpenAiStreamingScenario.cs                       # Interactive demo

wiki/
└── streaming.md                                     # Feature documentation (174 lines)
```

**Structure Decision**: Streaming is implemented as methods on the existing per-provider
chat completion clients (no separate streaming-only classes). The SSE line-parsing lives
in the shared `LlmHttpClient`, while JSON deserialization of provider-specific event
models lives in each provider project. This minimizes file count and keeps the streaming
implementation close to the non-streaming request logic it shares (message mapping,
model detection, endpoint routing).

## Complexity Tracking

No constitution violations. The feature adds no new projects, no new abstractions
beyond what the Feature Collection pattern already provides, and no external dependencies.
