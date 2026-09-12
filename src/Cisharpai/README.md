# Cisharpai

Core abstractions and shared logic for the Cisharpai unified LLM client library.

## What is Cisharpai?

Cisharpai provides a common interface for interacting with multiple LLM providers (OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere). Your application code depends only on this core package — provider-specific logic lives in separate packages.

## Key Interfaces

- **`IChatCompletionClient`** — unified chat completion across all providers
- **`IEmbeddingClient`** — unified text embedding across all providers

## Feature Collection Pattern

Optional capabilities are discovered at runtime via `IHasFeatures.Features.Get<T>()`:

| Feature | Description |
|---------|-------------|
| `IJsonOutputFeature` | JSON Mode and Structured Outputs |
| `IToolCallingFeature` | Function/tool calling |
| `IStreamingChatFeature` | Token-by-token streaming |
| `IGroundedChatFeature` | RAG with document citations |
| `IImageEmbeddingFeature` | Image embeddings |
| `IMultimodalEmbeddingFeature` | Mixed text + image embeddings |

## Quick Start

```csharp
using Cisharpai;
using Cisharpai.Models;

// Use any provider package to get an IChatCompletionClient
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Hello!")],
    Model: "gpt-4.1-nano");

var response = await client.GetChatCompletionAsync(request);
Console.WriteLine(response.Content);
```

## Design Principles

- **No exceptions for API errors** — responses use `IsSuccess`/`ErrorMessage`
- **Debuggability** — access `RawRequestJson`/`RawResponseJson` on any response
- **Immutability** — all DTOs are immutable `record` types
- **Escape hatch** — `ExtraParameters` deep-merges arbitrary JSON into requests

## Provider Packages

| Package | Provider |
|---------|----------|
| [Cisharpai.OpenAi](https://www.nuget.org/packages/Cisharpai.OpenAi) | OpenAI |
| [Cisharpai.Azure](https://www.nuget.org/packages/Cisharpai.Azure) | Azure OpenAI & Azure AI Inference |
| [Cisharpai.Anthropic](https://www.nuget.org/packages/Cisharpai.Anthropic) | Anthropic (Claude) |
| [Cisharpai.Cohere](https://www.nuget.org/packages/Cisharpai.Cohere) | Cohere |
| [Cisharpai.Testing](https://www.nuget.org/packages/Cisharpai.Testing) | Fake clients for unit testing |

## Links

- [GitHub](https://github.com/alkampfergit/cisharpai)
- [Documentation](https://github.com/alkampfergit/cisharpai/tree/main/wiki)
