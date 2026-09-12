---
name: cisharpai-expert
description: >
  Expert guidance for the Cisharpai .NET library — a unified HttpClient-based
  interface for OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, and Cohere
  LLM providers. Use when writing, debugging, or architecting code that uses
  Cisharpai clients, features, DTOs, DI registration, or provider-specific
  integrations. Activates on mentions of "Cisharpai", "IChatCompletionClient",
  "IEmbeddingClient", "IRerankerClient", provider setup, tool calling, streaming,
  JSON output, grounded chat, vision, embeddings, reranking, RAG ingestion, or
  fake clients for testing.
---

# Cisharpai Expert

## Overview

Cisharpai is a unified .NET client library providing a common `HttpClient`-based
interface for multiple LLM providers. Switching providers is a configuration
change — application code stays the same.

**Supported Providers:** OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere

**Key Design Principles:**

- **Unified Abstraction** — Same `IChatCompletionClient` / `IEmbeddingClient` / `IRerankerClient` for all providers that offer the capability
- **No Exceptions for API Errors** — `IsSuccess` + `ErrorMessage` on responses; exceptions only for network/config
- **Debuggability** — `RawResponseJson` / `RawRequestJson` on every response
- **Immutable DTOs** — Request/Response types are immutable records
- **Feature Collection Pattern** — Optional capabilities via `IHasFeatures.Features.Get<T>()`
- **Escape Hatch** — `ExtraParameters` deep-merges arbitrary JSON into requests
- **HTTP Resilience** — DI helpers add retry, timeout, and circuit-breaker policies; `Create(...)` requires explicit named `HttpClient` resilience registration

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
services.AddCohereChatClient(o => { o.ApiKey = "..."; });

// Cohere reranking
services.AddCohereRerankerClient(o => {
    o.ApiKey = "...";
    o.DefaultModel = CohereModels.Rerank.RerankV3_5;
});
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

## HTTP Resilience

Provider DI helpers automatically call `AddCisharpaiResilienceHandler()` on their `HttpClient` registrations. This applies to OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere, and embedding clients.

The standard handler uses `Microsoft.Extensions.Http.Resilience` / Polly with:

- Retries for transient failures: HTTP `408`, `429`, `5xx`, `HttpRequestException`, and timeout failures.
- `Retry-After` support, so `429 Too Many Requests` can delay according to the server-provided header.
- 3 retry attempts, 500 ms initial delay, exponential backoff, and jitter.
- 60 second per-attempt timeout and 90 second total request timeout.
- Circuit breaker with 120 second sampling, 20% failure ratio, minimum 10 requests, and 15 second break duration.

After retries are exhausted, API-level HTTP errors become normal response failures (`IsSuccess = false`, `ErrorMessage`, and raw response body when available). Network/configuration problems may still throw.

When using `Create(...)`, resilience is not added automatically. Register the named handler with resilience:

```csharp
services.AddHttpClient("cisharpai")
    .AddCisharpaiResilienceHandler();
```

For long-running streaming workloads, use `AddCisharpaiStreamingResilienceHandler()` on the streaming `HttpClient` registration. It removes the standard 60s/90s timeouts while keeping retry and circuit-breaker behavior.

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
- `Create` bypasses DI resilience handlers; add `services.AddHttpClient("cisharpai").AddCisharpaiResilienceHandler()` at startup if retries/timeouts/circuit breaking are needed.
- Azure OpenAI chat clients share learned routing fallbacks in-process per `(Endpoint, DeploymentName, ApiVersion)`, so later dynamically created clients reuse the working route after the first mismatch is discovered.

## DI Client Factory (runtime creation with resilience)

When providers are chosen at runtime AND you want full DI benefits (resilience handlers, HttpClient pooling), use `ICisharpaiClientFactory`. Each provider has a strongly-typed configuration class.

```csharp
// Startup — register factory with desired providers
services.AddCisharpaiClientFactory()
    .AddOpenAiSupport()
    .AddAnthropicSupport()
    .AddAzureOpenAiSupport()
    .AddAzureAiInferenceSupport()
    .AddCohereSupport();

// Runtime — create clients from configuration
var factory = serviceProvider.GetRequiredService<ICisharpaiClientFactory>();
var config = new OpenAiClientConfiguration { ApiKey = "sk-...", DefaultModel = "gpt-4o" };
var result = factory.CreateChatCompletionClient(config);
if (result.IsSuccess) { /* use result.Client */ }
else { /* result.ErrorMessage explains why */ }

// Reranking (Cohere only — other providers return IsSuccess=false)
var rerankResult = factory.CreateRerankerClient(new CohereClientConfiguration
{
    ApiKey = "...",
    DefaultModel = CohereModels.Rerank.RerankV3_5
});
```

**Configuration classes:** `OpenAiClientConfiguration`, `AnthropicClientConfiguration`, `AzureOpenAiClientConfiguration` (requires Endpoint + DeploymentName), `AzureAiInferenceClientConfiguration` (requires Endpoint + ModelId), `CohereClientConfiguration`.

**Error handling:** Returns `CisharpaiClientFactoryResult<T>` with `IsSuccess`/`ErrorMessage` — no exceptions for unregistered providers or unsupported capabilities.

**Testing:** Use `FakeClientFactoryProvider` from `Cisharpai.Testing` with `builder.AddFakeSupport()`.

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

### IRerankerClient

Reranking reorders candidate documents by relevance to a query — the second stage of a RAG
pipeline. **Cohere only**; other providers have no rerank API.

```csharp
public interface IRerankerClient : IHasFeatures
{
    Task<RerankResponse> RerankAsync(
        RerankRequest request, CancellationToken ct = default);
}
```

```csharp
string[] documents = [ /* candidates from your vector search */ ];

var response = await rerankClient.RerankAsync(new RerankRequest(
    Query: "What is the capital of France?",
    Documents: documents,
    TopN: 3));

if (!response.IsSuccess) { /* response.ErrorMessage */ }

foreach (var result in response.Results)
{
    // Index points back into YOUR documents array; Results are most-relevant-first
    Console.WriteLine($"{result.RelevanceScore:F4}  {documents[result.Index]}");
}
```

**Alternative hosting:** set `CohereClientOptions.BaseUrl` (e.g. an Azure AI Foundry deployment)
— the `rerank` path is appended relatively and nothing else changes.

**Cohere `priority`:** deliberately not on `RerankRequest` (no analogue at other providers). Use
`ExtraParameters: JsonSerializer.SerializeToElement(new { priority = 500 })` (integer 0–999, lower = higher priority).

**Gotcha:** if neither `RerankRequest.Model` nor `CohereClientOptions.DefaultModel` is set,
`RerankAsync` throws `InvalidOperationException` — that is a config error, not an API error.

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

**RerankRequest:**
- `Query`, `Documents` (string[]), `Model?`, `TopN?`, `MaxTokensPerDocument?`, `IncludeRawResponse`, `ExtraParameters?`

**RerankResponse:**
- `Results` (`RerankResult[]`, most relevant first), `Model`, `SearchUnits?`, `InputTokens?`, `IsSuccess`, `ErrorMessage`, raw payloads

**RerankResult:**
- `Index` (position in the request's `Documents`), `RelevanceScore` (provider-defined scale — compare within one response only)

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

Reranking is not a feature interface — it is its own top-level client (`IRerankerClient`),
implemented by Cohere only.
## RAG Ingestion (`Cisharpai.Rag`)

Use the separate `Cisharpai.Rag` package for ingestion with any `IEmbeddingClient`. This is independent of Cohere's `IGroundedChatFeature`; it does not provide storage, retrieval or generation.

- Root namespace `Cisharpai.Rag`: `IRagIngestionPipeline`, `RagIngestionPipeline`, `RagOptions`, `AddCisharpaiRag`.
- `.Chunking`: `ITextChunker.Chunk(RagDocument)`, `FixedSizeChunker`, `FixedSizeChunkerOptions`.
- `.Embeddings`: `IBulkEmbeddingProcessor.EmbedAsync(...)`, `BulkEmbeddingProcessor`, `BulkEmbeddingOptions`.
- `.Models`: `RagDocument(Id, Text)`, `TextChunk(DocumentId, Index, StartOffset, Text)`, `ChunkEmbedding(Chunk, Vector)`, `EmbeddingBatchResult`.
- Chunk defaults: 1024 Unicode scalar values and 128 overlap; require positive size and `0 <= overlap < size`. `StartOffset` uses UTF-16 units. Preserve whitespace and valid surrogate pairs; empty text yields no chunks. Callers own document ID uniqueness.
- Embedding defaults: batch size 32, `InputType = EmbeddingInputType.Document`, float encoding. Options include `Model`, `Dimensions`, `IncludeRawResponse`, `ExtraParameters`. Size is not a token limit.
- Direct composition: `new RagIngestionPipeline(new FixedSizeChunker(chunkOptions), new BulkEmbeddingProcessor(client, embeddingOptions))`.
- Register provider first, then `services.AddCisharpaiRag(o => { o.Chunking.ChunkSize = 1024; o.Embedding.BatchSize = 32; })`. Host configuration binding is the application's responsibility: `configuration.GetSection("Rag").Bind(o)` inside the callback.
- Keyed selection: `services.AddCisharpaiRag(sp => sp.GetRequiredKeyedService<IEmbeddingClient>("documents"), o => o.Embedding.Model = "text-embedding-3-small")`. Processors/pipelines are scoped; resolve within a service scope. Options are validated and snapshotted at construction/resolution; no live reload.
- `IngestAsync` accepts collections or async streams of documents; `EmbedAsync` accepts collections or async streams of chunks. Both take cancellation tokens and return async batch streams. Processing is sequential and buffers one batch plus the current document.
- Check `batch.IsSuccess` before reading `batch.Items`; each item has `Chunk` and `Vector`. `batch.Chunks`, zero-based `BatchIndex` and `Response` retain input identity and provider metadata/raw payloads. The first failed or malformed batch has empty items and stops processing; earlier successes remain available. No rollback/checkpoints or ingestion-level retries. Provider HTTP resilience remains independent.
- Cancellation and network/configuration exceptions propagate. Fake with existing `FakeEmbeddingClient`, queuing one float vector per expected chunk; a default single-vector response fails validation for multi-chunk batches.

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
- [references/reranking.md](references/reranking.md) — Relevance reranking with `IRerankerClient` (Cohere)
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
- `src/Cisharpai.Rag/` — Fixed-size chunking, bulk embeddings, document ingestion and configuration
- `src/Cisharpai.Testing/` — Fake clients for unit testing
- `src/Cisharpai.Tests/` — Unit tests (all providers)
- `src/Cisharpai.Integration.Tests/` — Integration tests (.NET 10 only)

## Troubleshooting

**Response returns `IsSuccess = false`:**
Set `IncludeRawResponse = true` on the request and inspect `RawResponseJson`.

**Feature returns `null` from `Get<T>()`:**
The provider doesn't support that feature. Check [references/provider-features.md](references/provider-features.md).

**Build targets:** Projects multitarget .NET 8.0 and .NET 10.
