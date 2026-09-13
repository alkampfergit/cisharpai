# Cisharpai Wiki

Cisharpai is a unified .NET client library providing a single `IChatCompletionClient` / `IEmbeddingClient` interface across multiple LLM providers. Switching providers is a configuration change — application code stays the same.

## Supported Providers

| Provider | Package | Chat | Embeddings | Reranking | Notes |
|----------|---------|------|------------|-----------|-------|
| OpenAI | `Cisharpai.OpenAi` | Yes | Yes | -- | Chat Completions, Responses API (GPT-5), reasoning models |
| Azure OpenAI | `Cisharpai.Azure` | Yes | Yes | -- | Deployment-based routing, Azure AD auth, Responses API (GPT-5) |
| Azure AI Inference | `Cisharpai.Azure` | Yes | Yes | -- | Model catalog: Phi, Llama, Mistral; image embeddings |
| Anthropic | `Cisharpai.Anthropic` | Yes | -- | -- | Claude model family, grounded chat (RAG) |
| Cohere | `Cisharpai.Cohere` | Yes | Yes | Yes | Grounded chat (RAG), multimodal embeddings, reranking |

## Key Design Principles

- **No exceptions for API errors** — check `response.IsSuccess` and `response.ErrorMessage`; exceptions are only for network/config failures
- **Feature Collection pattern** — optional capabilities (streaming, tool calling, JSON output, etc.) are discovered via `client.Features.Get<T>()`
- **Immutable DTOs** — `ChatCompletionRequest`, `LlmMessage`, and all response types are immutable records
- **Escape hatch** — `ExtraParameters` deep-merges arbitrary JSON into any request for provider-specific fields

## Contents

### Getting Started

- [Getting Started](getting-started.md) — installation, DI setup, first request, multi-provider patterns
- [OpenAI Provider](openai.md) — OpenAI-specific models, routing, reasoning models, Responses API

### Architecture

- [Feature Collection Pattern](feature-extensions.md) — how optional capabilities are discovered and accessed
- [Provider Feature Matrix](provider-features.md) — full support table across all providers

### Feature Guides

- [Streaming](streaming.md) — token-by-token streaming via `IStreamingChatFeature`
- [Tool Calling](tool-calling.md) — function calling via `IToolCallingFeature`
- [JSON Output](json-output.md) — JSON Mode and Structured Outputs via `IJsonOutputFeature`
- [Vision](vision.md) — sending images in chat messages
- [RAG Ingestion](rag.md) — fixed-size chunking, bounded bulk embeddings, host configuration and keyed providers
- [Embeddings](embeddings.md) — text, image, and multimodal embeddings
- [Reranking](reranking.md) — relevance reranking via `IRerankerClient` (Cohere)
- [Grounded Chat (RAG)](grounded-chat.md) — document grounding with citations via `IGroundedChatFeature` (Anthropic, Cohere)
- [Prompt Caching](prompt-caching.md) — cache usage reporting (all providers) and explicit cache control via `IPromptCachingFeature` (Anthropic)

### Operations

- [Logging](logging.md) — structured `ILogger` output and distributed tracing with `ActivitySource`
- [Runtime Configuration](runtime-configuration.md) — creating clients dynamically at request time (multi-tenant, runtime API keys)
- [Client Factory](factory.md) — `ICisharpaiClientFactory` for runtime client creation with full DI benefits (resilience, HttpClient management)

### Testing

- [Testing with Cisharpai](testing.md) — fake clients, response factories, request capture, DI helpers

### Reference

- [FAQ](qa.md) — frequently asked questions and verified answers
