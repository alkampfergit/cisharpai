# OpenAI quickstart

This guide shows how to call OpenAI using Cisharpai.

## Prerequisites

- An OpenAI API key
- A model name (for example: gpt-4.1-nano, gpt-5, o1-mini)

## 1) Register the OpenAI client

```csharp
using Cisharpai;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddOpenAiClient(options =>
{
    options.ApiKey = "YOUR_API_KEY";
    // Optional:
    // options.BaseUrl = "https://api.openai.com/v1/";
    // options.Organization = "org_...";
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatCompletionClient>();
```

## 2) Send a request

```csharp
using Cisharpai.Models;

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Write a short haiku about winter.")],
    Model: "gpt-4.1-nano",
    Temperature: 0.2,
    MaxTokens: 200,
    IncludeRawResponse: false);

var response = await client.GetChatCompletionAsync(request);
Console.WriteLine(response.Content);
```

## Model routing notes

The OpenAI implementation routes to different API shapes based on the model name:

- gpt-5 models use the Responses API.
- o1/o3/o4 models use reasoning mode.
- All other models use the legacy Chat Completions API.

Optional settings for GPT-5 Responses API can be configured on `OpenAiClientOptions`:

- `ReasoningEffort`
- `TextVerbosity`

## Embeddings

OpenAI also supports text embeddings via a separate client registration:

```csharp
services.AddOpenAiEmbeddingClient(options =>
{
    options.ApiKey = "YOUR_API_KEY";
});

var client = provider.GetRequiredService<IEmbeddingClient>();
var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(
    Input: ["Hello world"],
    Model: "text-embedding-3-small",
    Dimensions: 256));
```

See [Embeddings](embeddings.md) for full documentation.

## Troubleshooting

- If you need the raw JSON, set `IncludeRawResponse: true` and read `RawResponseJson` on the response.
- For a working example, see [src/Cisharp.Console/Scenarios/OpenAiChatScenario.cs](../src/Cisharp.Console/Scenarios/OpenAiChatScenario.cs).
