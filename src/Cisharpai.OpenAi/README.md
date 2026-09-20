# Cisharpai.OpenAi

OpenAI provider for the [Cisharpai](https://www.nuget.org/packages/Cisharpai) unified LLM client library.

## Features

- Chat completions (GPT-4, GPT-4.1, GPT-4.5)
- Reasoning models (o1, o3, o4-mini) with configurable reasoning effort
- Responses API (GPT-5) with status/incomplete handling
- Text embeddings (text-embedding-3-small, text-embedding-3-large, ada-002)
- JSON Mode and Structured Outputs (with refusal handling)
- Tool calling / function calling (all ToolChoice variants)
- Vision (image file paths and base64)
- Streaming (token-by-token via SSE)
- Built-in HTTP resilience (retries, timeouts)

## Quick Start

```csharp
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddOpenAiClient(options =>
{
    options.ApiKey = "YOUR_API_KEY";
    options.DefaultModel = OpenAiModels.Chat.Gpt4_1Nano;
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatCompletionClient>();

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Say hello in one sentence.")],
    Temperature: 0.2,
    MaxTokens: 100);

var response = await client.GetChatCompletionAsync(request);
Console.WriteLine(response.Content);
```

## Embeddings

```csharp
services.AddOpenAiEmbeddingClient(options =>
{
    options.ApiKey = "YOUR_API_KEY";
    options.DefaultModel = OpenAiModels.Embedding.TextEmbedding3Small;
});

var embeddingClient = provider.GetRequiredService<IEmbeddingClient>();
var result = await embeddingClient.GetEmbeddingAsync(
    new EmbeddingRequest(Input: ["Hello world"]));
```

## Available Models

**Chat:** `Gpt4_1`, `Gpt4_1Mini`, `Gpt4_1Nano`, `Gpt4o`, `Gpt4oMini`, `Gpt4_5`, `O3`, `O3Mini`, `O3Pro`, `O4Mini`, `O1`, `O1Mini`

**Embedding:** `TextEmbedding3Small`, `TextEmbedding3Large`, `TextEmbeddingAda002`

## Links

- [GitHub](https://github.com/alkampfergit/cisharpai)
- [OpenAI Quickstart](https://github.com/alkampfergit/cisharpai/blob/main/wiki/openai.md)
- [Provider Feature Matrix](https://github.com/alkampfergit/cisharpai/blob/main/wiki/provider-features.md)
