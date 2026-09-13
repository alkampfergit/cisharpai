# Cisharpai.Cohere

Cohere provider for the [Cisharpai](https://www.nuget.org/packages/Cisharpai) unified LLM client library.

## Features

### Chat
- Chat completions via Cohere v2 Chat API
- JSON Mode and Structured Outputs (`response_format` with `json_schema`)
- Tool calling with uppercase ToolChoice mapping and `strict_tools` flag
- Grounded chat (RAG) with document citations via `IGroundedChatFeature`
- Streaming (token-by-token via SSE)

### Embeddings
- Text embeddings (Embed v3 models)
- Image embeddings via `IImageEmbeddingFeature`
- Multimodal embeddings (Embed v4) — mixed text + image inputs, Matryoshka dimension control via `IMultimodalEmbeddingFeature`

## Quick Start — Chat

```csharp
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.Cohere;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCohereChatClient(options =>
{
    options.ApiKey = "YOUR_API_KEY";
    options.DefaultModel = CohereModels.Chat.CommandA;
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatCompletionClient>();

var response = await client.GetChatCompletionAsync(
    new ChatCompletionRequest(
        Messages: [new LlmMessage(LlmRole.User, "Hello!")]));
```

## Quick Start — Embeddings

```csharp
services.AddCohereEmbeddingClient(options =>
{
    options.ApiKey = "YOUR_API_KEY";
    options.DefaultModel = CohereModels.Embedding.EmbedV4;
});

var embeddingClient = provider.GetRequiredService<IEmbeddingClient>();
var result = await embeddingClient.GetEmbeddingAsync(
    new EmbeddingRequest(Input: ["Hello world"], InputType: "search_document"));
```

## Grounded Chat (RAG)

Cohere supports `IGroundedChatFeature` — pass documents and get back citations with character offsets:

```csharp
var feature = ((IHasFeatures)client).Features.Get<IGroundedChatFeature>();
var result = await feature!.GetGroundedChatCompletionAsync(request,
    new GroundedChatOptions(
        Documents: [new DocumentChunk("doc1", Text: "The capital of France is Paris.")],
        CitationMode: CitationMode.Accurate));
```

## Available Models

**Chat:** `CommandA`, `CommandRPlus`, `CommandR`

**Embedding:** `EmbedV4`, `EmbedEnglishV3`, `EmbedMultilingualV3`, `EmbedEnglishLightV3`, `EmbedMultilingualLightV3`

## Keyed Services

```csharp
services.AddCohereChatClient("chat", options => { /* ... */ });
services.AddCohereEmbeddingClient("embed", options => { /* ... */ });
```

## Links

- [GitHub](https://github.com/alkampfergit/cisharpai)
- [Grounded Chat (RAG) Guide](https://github.com/alkampfergit/cisharpai/blob/main/wiki/grounded-chat.md)
- [Provider Feature Matrix](https://github.com/alkampfergit/cisharpai/blob/main/wiki/provider-features.md)
