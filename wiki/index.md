# Cisharpai Wiki

Welcome to the Cisharpai wiki. This is the starting point for learning how to use the library and its provider integrations.

## What is Cisharpai?

Cisharpai is a unified .NET client library that provides shared interfaces for interacting with multiple LLM providers. You can swap providers with minimal code changes by keeping your application code focused on the shared abstractions.

### Supported Providers

| Provider | Package | Capabilities |
|----------|---------|-------------|
| OpenAI | `Cisharpai.OpenAi` | Chat, embeddings, JSON output, tool calling, vision, streaming, reasoning models, Responses API (GPT-5) |
| Azure OpenAI | `Cisharpai.Azure` | Chat, embeddings, JSON output, tool calling, vision, streaming, reasoning models |
| Azure AI Inference | `Cisharpai.Azure` | Chat, embeddings, image embeddings, JSON output, tool calling, vision, streaming |
| Anthropic | `Cisharpai.Anthropic` | Chat, JSON output, tool calling, vision, streaming |
| Cohere | `Cisharpai.Cohere` | Chat, embeddings, image embeddings, multimodal embeddings, JSON output, tool calling, grounded chat (RAG), streaming |

### Key Features

- **Chat Completions** -- unified `IChatCompletionClient` interface across all providers
- **Text Embeddings** -- unified `IEmbeddingClient` interface (OpenAI, Azure OpenAI, Azure AI Inference, Cohere)
- **JSON Output** -- JSON Mode and Structured Outputs via `IJsonOutputFeature`
- **Tool Calling** -- function calling via `IToolCallingFeature` across all providers
- **Vision** -- send images in messages via `LlmMessage.WithImage()` / `LlmMessage.WithBase64Image()`
- **Streaming** -- token-by-token streaming via `IStreamingChatFeature` across all providers
- **Grounded Chat (RAG)** -- document grounding with citations via `IGroundedChatFeature` (Cohere)
- **Image Embeddings** -- via `IImageEmbeddingFeature` (Azure AI Inference, Cohere)
- **Multimodal Embeddings** -- mixed text + image inputs via `IMultimodalEmbeddingFeature` (Cohere)
- **Feature Discovery** -- optional capabilities discovered via the Feature Collection pattern

## Contents

### Getting Started

- [Getting Started](getting-started.md) -- setup and basic usage
- [OpenAI Quickstart](openai.md) -- OpenAI-specific guide

### Feature Guides

- [Embeddings](embeddings.md) -- text, image, and multimodal embeddings
- [JSON Output](json-output.md) -- JSON Mode and Structured Outputs
- [Tool Calling](tool-calling.md) -- function calling across providers
- [Grounded Chat (RAG)](grounded-chat.md) -- document grounding with citations
- [Vision](vision.md) -- sending images in chat messages
- [Streaming](streaming.md) -- streaming chat completions token-by-token

### Reference

- [Provider Feature Matrix](provider-features.md) -- complete feature support table
- [Feature Extensions](feature-extensions.md) -- Feature Collection pattern documentation
