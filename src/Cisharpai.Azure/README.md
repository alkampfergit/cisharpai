# Cisharpai.Azure

Azure AI provider for the [Cisharpai](https://www.nuget.org/packages/Cisharpai) unified LLM client library. Covers both **Azure OpenAI Service** and **Azure AI Inference** (model-as-a-service).

## Features

### Azure OpenAI
- Chat completions with all Azure OpenAI deployments
- Reasoning models (o1, o3, o4, GPT-5) with automatic `max_completion_tokens` handling and optional `ReasoningEffort`
- Text embeddings (ada-002, text-embedding-3-small/large)
- JSON Mode and Structured Outputs
- Tool calling / function calling
- Vision (image file paths and base64)
- Streaming (token-by-token via SSE)
- API key and Azure AD (Entra ID) authentication

### Azure AI Inference
- Chat completions with Azure AI model catalog (Phi-3, Llama-3, Mistral, etc.)
- Reasoning model detection and appropriate request formatting
- Text and image embeddings (with `IImageEmbeddingFeature`)
- JSON Mode and Structured Outputs
- Tool calling / function calling
- Vision (image file paths and base64)
- Streaming (token-by-token via SSE)

## Quick Start — Azure OpenAI

```csharp
using Cisharpai;
using Cisharpai.Models;
using Cisharpai.Azure.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAzureOpenAiClient(options =>
{
    options.Endpoint = "https://myresource.openai.azure.com";
    options.ApiKey = "YOUR_API_KEY";
    options.DeploymentName = "gpt-4o";
    // Optional for o1/o3/o4/gpt-5 deployments:
    // options.ReasoningEffort = "low";
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatCompletionClient>();

var response = await client.GetChatCompletionAsync(
    new ChatCompletionRequest(
        Messages: [new LlmMessage(LlmRole.User, "Hello!")]));
```

`ReasoningEffort` is sent only for reasoning deployments. For provider parameters that are not typed yet, or to override a typed value per request, use `ChatCompletionRequest.ExtraParameters`; it deep-merges into the final JSON request.

## Quick Start — Azure AI Inference

```csharp
services.AddAzureAiInferenceChatCompletion(options =>
{
    options.Endpoint = "https://mymodel.eastus.models.ai.azure.com";
    options.ApiKey = "YOUR_API_KEY";
    options.ModelId = "Phi-3-mini-4k-instruct";
});
```

## Authentication

Both API key and Azure AD (Bearer token) authentication are supported. Omit `ApiKey` to use `Azure.Identity` DefaultAzureCredential with scope `https://cognitiveservices.azure.com/.default`.

## Keyed Services

.NET 8 keyed DI is supported for registering multiple clients:

```csharp
services.AddAzureOpenAiClient("gpt4o", options => { /* ... */ });
services.AddAzureOpenAiClient("gpt4mini", options => { /* ... */ });
```

## Links

- [GitHub](https://github.com/alkampfergit/cisharpai)
- [Provider Feature Matrix](https://github.com/alkampfergit/cisharpai/blob/main/wiki/provider-features.md)
