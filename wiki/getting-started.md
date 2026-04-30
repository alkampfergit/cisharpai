# Getting Started

Cisharpai is a unified .NET client library that provides a single, consistent interface for interacting with multiple LLM providers. Switching providers is a configuration change — your application code stays the same.

## Design Principles

- **Unified abstraction** — `IChatCompletionClient` and `IEmbeddingClient` are identical across all providers.
- **No exceptions for API errors** — clients return `IsSuccess=false` and `ErrorMessage` instead of throwing. Exceptions only occur for network or configuration failures.
- **Optional capabilities via feature discovery** — streaming, tool calling, JSON output, and other provider-specific features are accessed through `client.Features.Get<T>()` and return `null` when unsupported.
- **Debuggability** — every response includes `RawRequestJson` and `RawResponseJson` when `IncludeRawResponse: true` is set on the request.
- **Escape hatch** — `ExtraParameters` on any request deep-merges arbitrary JSON into the outbound body for provider-specific fields not yet in the unified API.

## Installation

Add the core package and one or more provider packages:

```xml
<!-- Core abstractions — always required -->
<PackageReference Include="Cisharpai" />

<!-- Pick one or more providers -->
<PackageReference Include="Cisharpai.OpenAi" />
<PackageReference Include="Cisharpai.Azure" />
<PackageReference Include="Cisharpai.Anthropic" />
<PackageReference Include="Cisharpai.Cohere" />

<!-- For unit testing -->
<PackageReference Include="Cisharpai.Testing" />
```

## Registering a Provider

Use the DI extension methods in `Program.cs` or your service configuration:

```csharp
// OpenAI
services.AddOpenAiClient(o =>
{
    o.ApiKey = "sk-...";
    o.DefaultModel = "gpt-4.1";    // optional fallback model
});

// Azure OpenAI
services.AddAzureOpenAiClient(o =>
{
    o.Endpoint = "https://myresource.openai.azure.com";
    o.DeploymentName = "gpt-4o";
    o.ApiKey = "...";              // or use o.TokenCredential for Azure AD
});

// Azure AI Inference (Phi, Llama, Mistral, etc.)
services.AddAzureAiInferenceClient(o =>
{
    o.Endpoint = "https://myendpoint.inference.ai.azure.com";
    o.ModelId = "Phi-4";
    o.ApiKey = "...";
});

// Anthropic
services.AddAnthropicClient(o =>
{
    o.ApiKey = "sk-ant-...";
    o.DefaultModel = "claude-sonnet-4-5-20250514";
});

// Cohere
services.AddCohereClient(o =>
{
    o.ApiKey = "...";
    o.DefaultModel = "command-a-08-2025";
});
```

## Sending a Request

Resolve `IChatCompletionClient` from DI and use it the same way regardless of provider:

```csharp
using Cisharpai.Models;

var client = provider.GetRequiredService<IChatCompletionClient>();

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Say hello in one sentence.")],
    Model: "gpt-4.1-nano",       // optional if DefaultModel is set
    Temperature: 0.2,
    MaxTokens: 100);

var response = await client.GetChatCompletionAsync(request);

if (response.IsSuccess)
    Console.WriteLine(response.Content);
else
    Console.WriteLine($"Error: {response.ErrorMessage}");
```

## Using Optional Features

Optional capabilities are discovered at runtime through the Feature Collection pattern. Always null-check before use:

```csharp
// Streaming
if (client.Features.Get<IStreamingChatFeature>() is { } streaming)
{
    await foreach (var chunk in streaming.GetChatCompletionStreamAsync(request))
        Console.Write(chunk.Content);
}

// Tool calling
if (client.Features.Get<IToolCallingFeature>() is { } tools)
{
    var response = await tools.GetChatCompletionWithToolsAsync(request, toolOptions);
}

// JSON output
if (client.Features.Get<IJsonOutputFeature>() is { } json)
{
    var response = await json.GetChatCompletionWithJsonOutputAsync(request, jsonOptions);
}
```

See [Feature Collection Pattern](feature-extensions.md) for the full discovery API, and [Provider Feature Matrix](provider-features.md) for which providers support which features.

## Multiple Providers in One App

Use keyed DI when you need more than one provider simultaneously:

```csharp
services.AddOpenAiClient("openai", o => { o.ApiKey = "sk-..."; });
services.AddAnthropicClient("anthropic", o => { o.ApiKey = "sk-ant-..."; });

// Resolve by key
var openAi = provider.GetRequiredKeyedService<IChatCompletionClient>("openai");
var claude = provider.GetRequiredKeyedService<IChatCompletionClient>("anthropic");
```

## Dynamic / Runtime Configuration

If API keys or endpoints are not known at startup (e.g. multi-tenant systems), use the static `Create` factory methods instead of DI registration. See [Runtime Configuration](runtime-configuration.md).

## Next Steps

- [Provider Feature Matrix](provider-features.md) — full capability comparison across providers
- [Feature Collection Pattern](feature-extensions.md) — how optional features work
- [Streaming](streaming.md) — token-by-token streaming
- [Tool Calling](tool-calling.md) — function calling across providers
- [JSON Output](json-output.md) — JSON Mode and Structured Outputs
- [Vision](vision.md) — sending images in chat messages
- [Embeddings](embeddings.md) — text, image, and multimodal embeddings
- [Grounded Chat (RAG)](grounded-chat.md) — document grounding with citations (Cohere)
- [Logging](logging.md) — structured logs and distributed tracing
- [Testing with Cisharpai](testing.md) — fake clients for unit tests
- [Runtime Configuration](runtime-configuration.md) — dynamic client creation
