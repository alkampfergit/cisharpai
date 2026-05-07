---
name: cisharpai-expert
description: >
  Expert guidance for the Cisharpai .NET library — a unified HttpClient-based
  interface for OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, and Cohere
  LLM providers. Use when writing, debugging, or architecting code that uses
  Cisharpai clients, features, DTOs, DI registration, or provider-specific
  integrations. Activates on mentions of "Cisharpai", "IChatCompletionClient",
  "IEmbeddingClient", provider setup, tool calling, streaming, JSON output,
  grounded chat, vision, embeddings, or fake clients for testing.
---

# Cisharpai Expert

## Overview

Cisharpai is a unified .NET client library providing a common `HttpClient`-based
interface for multiple LLM providers. Switching providers is a configuration
change — application code stays the same.

**Supported Providers:** OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere

**Key Design Principles:**

- **Unified Abstraction** — Same `IChatCompletionClient` / `IEmbeddingClient` for all providers
- **No Exceptions for API Errors** — `IsSuccess` + `ErrorMessage` on responses; exceptions only for network/config
- **Debuggability** — `RawResponseJson` / `RawRequestJson` on every response
- **Immutable DTOs** — Request/Response types are immutable records
- **Feature Collection Pattern** — Optional capabilities via `IHasFeatures.Features.Get<T>()`
- **Escape Hatch** — `ExtraParameters` deep-merges arbitrary JSON into requests

## Quick Start

### 1. Install Packages

```xml
<!-- Core (always needed) -->
<PackageReference Include="Cisharpai" />
<!-- Pick your provider -->
<PackageReference Include="Cisharpai.OpenAi" />
<!-- or Cisharpai.Azure, Cisharpai.Anthropic, Cisharpai.Cohere -->
```

### 2. Register via DI

```csharp
// OpenAI
services.AddOpenAiClient(o => { o.ApiKey = "sk-..."; });

// Azure OpenAI
services.AddAzureOpenAiClient(o => {
    o.Endpoint = "https://myresource.openai.azure.com";
    o.DeploymentName = "gpt-4o";
    o.ApiKey = "...";
    // Optional when the deployment name is opaque:
    // o.ModelName = "gpt-5";
});

// Anthropic
services.AddAnthropicClient(o => { o.ApiKey = "sk-ant-..."; });

// Cohere
services.AddCohereClient(o => { o.ApiKey = "..."; });
```

### 3. Send a Request

```csharp
using Cisharpai.Models;

var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Hello!")],
    Model: "gpt-4o");

var response = await client.GetChatCompletionAsync(request);

if (response.IsSuccess)
    Console.WriteLine(response.Content);
else
    Console.WriteLine($"Error: {response.ErrorMessage}");
```

## Runtime Client Creation (no DI required)

When API keys or endpoints are not known at startup (multi-tenant apps, user-provided credentials), use the static `Create` factory method on each client. Register a single pooled handler once; create client instances on demand.

```csharp
// Program.cs — once at startup (connection pool only, no auth)
services.AddHttpClient("cisharpai");

// At request time — inject IHttpMessageHandlerFactory
var client = OpenAiChatCompletionClient.Create(
    handlerFactory,
    new OpenAiClientOptions { ApiKey = runtimeKey, DefaultModel = "gpt-4o" },
    loggerFactory: loggerFactory);
```

All 9 clients support `Create`. Azure providers add an optional `TokenCredential` parameter for Azure AD auth. See [references/runtime-configuration.md](references/runtime-configuration.md) for all signatures and a multi-provider dispatch example.

**Key notes:**
- Client instances are cheap; TCP connections are pooled in the handler.
- `Create` bypasses DI resilience handlers — add `services.AddHttpClient("cisharpai").AddCisharpaiResilienceHandler()` at startup if needed.
- Azure OpenAI chat clients share learned routing fallbacks in-process per `(Endpoint, DeploymentName, ApiVersion)`, so later dynamically created clients reuse the working route after the first mismatch is discovered.

## Core Interfaces

### IChatCompletionClient

```csharp
public interface IChatCompletionClient : IHasFeatures
{
    Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request, CancellationToken ct = default);
}
```

### IEmbeddingClient

```csharp
public interface IEmbeddingClient : IHasFeatures
{
    Task<EmbeddingResponse> GetEmbeddingsAsync(
        EmbeddingRequest request, CancellationToken ct = default);
}
```

### Feature Discovery

Optional capabilities are accessed via the Feature Collection Pattern:

```csharp
// Check if provider supports streaming
var streaming = client.Features.Get<IStreamingChatFeature>();
if (streaming is not null)
{
    await foreach (var chunk in streaming.GetChatCompletionStreamAsync(request))
        Console.Write(chunk.Content);
}
```

## Core DTOs (Immutable Records)

**ChatCompletionRequest:**
- Positional immutable record: `new ChatCompletionRequest(Messages: [...], Model: "gpt-4o")`
- Properties: `Messages`, `Model?`, `Temperature?`, `MaxTokens?`, `IncludeRawResponse`, `ExtraParameters?`

**ChatCompletionResponse:**
- `Content`, `Usage`, `IsSuccess`, `ErrorMessage`, `RawResponseJson`, `RawRequestJson`, `Refusal`

**LlmMessage:**
- `Role`, `Content`, `ContentParts`, `ToolCallId`, `ToolCalls`
- Use `LlmRole.User`, `LlmRole.Assistant`, `LlmRole.System`, `LlmRole.Tool`; do not pass string roles.
- Factory: `LlmMessage.WithImage(text, filePath)`, `LlmMessage.WithBase64Image(text, base64, mediaType)`

**EmbeddingRequest:**
- `Input` (string[]), `Model?`, `InputType?`, `Dimensions?`, `EncodingFormat?`, `ExtraParameters?`

**EmbeddingResponse:**
- `Embeddings` (float[][]), `Dimensions`, `Model`, `TotalTokens`, `IsSuccess`, `ErrorMessage`

## Feature Interfaces

All feature interfaces live in the `Cisharpai.Features.Chat` namespace (not `Cisharpai.Features`).

| Feature | Interface | Providers |
|---------|-----------|-----------|
| JSON Output | `IJsonOutputFeature` | All 5 |
| Tool Calling | `IToolCallingFeature` | All 5 |
| Streaming | `IStreamingChatFeature` | All 5 |
| Grounded Chat | `IGroundedChatFeature` | Cohere only |
| Image Embedding | `IImageEmbeddingFeature` | Azure AI Inference, Cohere |
| Multimodal Embedding | `IMultimodalEmbeddingFeature` | Cohere only |

## Provider-Specific Guides

Each provider has unique setup, model routing, and quirks.
See the reference files for detailed information:

- [references/openai.md](references/openai.md) — OpenAI setup, model routing, Responses API
- [references/azure.md](references/azure.md) — Azure OpenAI + Azure AI Inference setup, auth
- [references/anthropic.md](references/anthropic.md) — Anthropic setup, raw base64 images, event SSE
- [references/cohere.md](references/cohere.md) — Cohere setup, grounded chat, embed v3/v4

## Feature Guides

- [references/tool-calling.md](references/tool-calling.md) — Define tools, handle calls, multi-turn
- [references/json-output.md](references/json-output.md) — JSON Mode vs Structured Outputs
- [references/streaming.md](references/streaming.md) — Token-by-token streaming
- [references/vision.md](references/vision.md) — Image input across providers
- [references/embeddings.md](references/embeddings.md) — Text, image, and multimodal embeddings
- [references/grounded-chat.md](references/grounded-chat.md) — RAG with citations (Cohere)
- [references/testing.md](references/testing.md) — Fake clients, response queues, DI
- [references/provider-features.md](references/provider-features.md) — Complete feature support matrix
- [references/runtime-configuration.md](references/runtime-configuration.md) — Dynamic client creation at runtime (multi-tenant, runtime API keys)

## Error Handling Pattern

```csharp
var response = await client.GetChatCompletionAsync(request);

if (!response.IsSuccess)
{
    // API-level error — no exception thrown
    logger.LogError("LLM error: {Error}", response.ErrorMessage);
    // Inspect raw response for debugging
    if (response.RawResponseJson is not null)
        logger.LogDebug("Raw: {Raw}", response.RawResponseJson);
    return;
}

// Success path
var content = response.Content;
```

## ExtraParameters Escape Hatch

Deep-merge arbitrary JSON into the provider request for bleeding-edge features:

```csharp
var request = new ChatCompletionRequest(
    Messages: [new LlmMessage(LlmRole.User, "Hello")],
    ExtraParameters: JsonDocument.Parse("""
    {
        "top_p": 0.9,
        "presence_penalty": 0.6
    }
    """).RootElement);
```

## Consumption Pitfalls

- Import both `Cisharpai` for interfaces and `Cisharpai.Models` for DTOs.
- `ChatCompletionRequest`, `LlmMessage`, `ToolDefinition`, `ToolCallingOptions`, and `JsonOutputOptions` are immutable positional records. Prefer constructor/named-argument syntax, not object initializers.
- `LlmMessage` takes `LlmRole`, not a string. Use `new LlmMessage(LlmRole.User, "...")`.
- Tool calling uses `IToolCallingFeature.GetChatCompletionWithToolsAsync(...)`.
- JSON output uses `IJsonOutputFeature.GetChatCompletionWithJsonOutputAsync(...)`.
- `FakeChatCompletionClient` queues responses with methods such as `EnqueueResponse(...)`, not a public `ResponseQueue` property.
- `JsonOutputMode` has two values: `JsonMode` (json_object, no schema) and `JsonSchema` (strict schema enforcement). There is no `JsonObject` value.
- `JsonOutputOptions` positional record signature: `(JsonOutputMode Mode, string? SchemaName = null, string? SchemaDescription = null, string? JsonSchema = null, bool Strict = true)`. `SchemaName` and `JsonSchema` are required when `Mode == JsonSchema`.
- `IJsonOutputFeature` is in `Cisharpai.Features.Chat`, not `Cisharpai.Features`.

## Project Structure

- `src/Cisharpai/` — Core abstractions, interfaces, models, helpers
- `src/Cisharpai.OpenAi/` — OpenAI provider
- `src/Cisharpai.Azure/` — Azure OpenAI + Azure AI Inference
- `src/Cisharpai.Anthropic/` — Anthropic provider
- `src/Cisharpai.Cohere/` — Cohere provider
- `src/Cisharpai.Testing/` — Fake clients for unit testing
- `src/Cisharpai.Tests/` — Unit tests (all providers)
- `src/Cisharpai.Integration.Tests/` — Integration tests (.NET 10 only)

## Troubleshooting

**Response returns `IsSuccess = false`:**
Set `IncludeRawResponse = true` on the request and inspect `RawResponseJson`.

**Feature returns `null` from `Get<T>()`:**
The provider doesn't support that feature. Check [references/provider-features.md](references/provider-features.md).

**Build targets:** Projects multitarget .NET 8.0 and .NET 10.
