# Implementation Plan: DI Client Factory

**Branch**: `009-di-client-factory` | **Date**: 2026-05-15 | **Spec**: [spec.md](../../.specify/specs/009-di-client-factory/spec.md)

**Input**: Feature specification from `.specify/specs/009-di-client-factory/spec.md`

## Summary

Add a core `ICisharpaiClientFactory` interface and `RuntimeClientConfiguration` record to `Cisharpai/` that lets consumers create `IChatCompletionClient` and `IEmbeddingClient` instances at runtime from a provider-agnostic configuration. Each provider project registers a factory descriptor via `ICisharpaiClientFactoryBuilder` fluent API. Factory-created clients use `IHttpClientFactory` with the standard resilience handler, identical to the existing `Add*Client` DI registrations.

## Technical Context

**Language/Version**: C# on .NET 8.0 / .NET 10 (multi-target)

**Primary Dependencies**: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Http.Resilience` (Polly), `System.Text.Json`

**Storage**: N/A

**Testing**: NUnit + NSubstitute, multi-target .NET 8.0 / .NET 10

**Target Platform**: Cross-platform .NET library

**Project Type**: Library (NuGet package)

**Performance Goals**: Factory method overhead negligible (< 1ms) — dominated by HttpClient creation via IHttpClientFactory

**Constraints**: No new project files — all changes go into existing projects. Core interface in `Cisharpai/`, provider registrations in each provider project.

**Scale/Scope**: 5 providers × 2 client types (chat + embedding where supported) = ~8 factory paths

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Unified Abstraction | PASS | Factory returns `IChatCompletionClient` / `IEmbeddingClient` — same interfaces |
| II. No Exceptions for API Errors | PASS | Factory returns result wrapper with `IsSuccess`/`ErrorMessage` for unregistered providers |
| III. Debuggability First | PASS | No change to response objects; factory delegates to existing clients |
| IV. Immutability | PASS | `RuntimeClientConfiguration` is an immutable record |
| V. Test-Driven Quality | PASS | Full test coverage planned for all providers |
| VI. Multi-Target Compatibility | PASS | No new projects, existing multi-target applies |
| VII. Documentation as Deliverable | PASS | Wiki, RELEASE_NOTES.md, project_overview.md updates planned |

No violations. No complexity tracking needed.

## Project Structure

### Documentation (this feature)

```text
specs/009-di-client-factory/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── factory-api.md
└── tasks.md             # Phase 2 output (created by /speckit-tasks)
```

### Source Code (repository root)

```text
src/
├── Cisharpai/                          # Core library
│   ├── ICisharpaiClientFactory.cs      # NEW — factory interface
│   ├── ICisharpaiClientFactoryBuilder.cs # NEW — fluent registration builder
│   ├── CisharpaiClientFactory.cs       # NEW — default implementation
│   ├── CisharpaiClientFactoryResult.cs # NEW — result wrapper
│   ├── RuntimeClientConfiguration.cs   # NEW — runtime config record
│   ├── CisharpaiProvider.cs            # NEW — provider enum
│   ├── IClientFactoryProvider.cs       # NEW — provider descriptor interface
│   └── CisharpaiClientFactoryExtensions.cs # NEW — IServiceCollection extension
│
├── Cisharpai.OpenAi/
│   └── OpenAiClientFactoryProvider.cs  # NEW — OpenAI factory descriptor
│   └── OpenAiFactoryBuilderExtensions.cs # NEW — builder.AddOpenAiSupport()
│
├── Cisharpai.Azure/
│   ├── AzureOpenAi/
│   │   └── AzureOpenAiClientFactoryProvider.cs  # NEW
│   ├── AzureAiInference/
│   │   └── AzureAiInferenceClientFactoryProvider.cs # NEW
│   └── Extensions/
│       └── AzureFactoryBuilderExtensions.cs # NEW — builder.AddAzureOpenAiSupport() + AddAzureAiInferenceSupport()
│
├── Cisharpai.Anthropic/
│   └── AnthropicClientFactoryProvider.cs # NEW
│   └── AnthropicFactoryBuilderExtensions.cs # NEW
│
├── Cisharpai.Cohere/
│   └── CohereClientFactoryProvider.cs  # NEW
│   └── CohereFactoryBuilderExtensions.cs # NEW
│
├── Cisharpai.Testing/
│   └── FakeClientFactoryProvider.cs    # NEW — fake factory for testing
│   └── FakeFactoryBuilderExtensions.cs # NEW
│
└── Cisharpai.Tests/
    └── Factory/                        # NEW test folder
        ├── RuntimeClientConfigurationTests.cs
        ├── CisharpaiClientFactoryTests.cs
        ├── OpenAiFactoryTests.cs
        ├── AnthropicFactoryTests.cs
        ├── AzureOpenAiFactoryTests.cs
        ├── AzureAiInferenceFactoryTests.cs
        ├── CohereFactoryTests.cs
        └── FakeClientFactoryTests.cs
```

**Structure Decision**: No new projects — all new types go into existing projects to avoid changing the NuGet package topology. The factory interface and core types live in `Cisharpai/` (the core abstractions package). Provider-specific factory descriptors live alongside existing provider code.
