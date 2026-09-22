---
name: cisharpai-expert
description: >
  Expert guidance for the Cisharpai .NET library — a unified HttpClient-based
  interface for OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, and Cohere
  LLM providers. Use when writing, debugging, or architecting code that uses
  Cisharpai clients, features, DTOs, DI registration, or provider-specific
  integrations. Activates on mentions of "Cisharpai", "IChatCompletionClient",
  "IEmbeddingClient", "IRerankerClient", "ITokenCounter", "IRetriever", provider setup,
  tool calling, streaming, JSON output, grounded chat, web search, vision, embeddings,
  reranking, token counting, RAG ingestion, retrieval, rank fusion, or fake clients for testing.
---

# Cisharpai Expert

## Overview

Cisharpai is a unified .NET client library providing a common `HttpClient`-based
interface for multiple LLM providers. Switching providers is a configuration
change — application code stays the same.

**Supported Providers:** OpenAI, Azure OpenAI, Azure AI Inference, Anthropic, Cohere

**Key Design Principles:**

- **Unified Abstraction** — Same `IChatCompletionClient` / `IEmbeddingClient` / `IRerankerClient` / `ITokenCounter` for all providers that offer the capability
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

### ITokenCounter

Counts tokens in a string for a specific model's tokenizer. Each instance is constructed for one
model — the model is baked in, not passed per call.

```csharp
public interface ITokenCounter
{
    ValueTask<int> CountAsync(string text, CancellationToken ct = default);
}
```

**Local counter (`TiktokenCounter` in `Cisharpai.Rag.Tokenizers`):** offline, synchronous, thread-safe.
Supports OpenAI-compatible tokenizers only (gpt-4o = o200k_base, gpt-4 = cl100k_base).
Install the separate `Cisharpai.Rag.Tokenizers` package to use it.

```csharp
using Cisharpai.Rag.Tokenization;

var counter = new TiktokenCounter("gpt-4o");
int count = counter.CountTokens("Hello, world!");          // sync
int countAsync = await counter.CountAsync("Hello, world!"); // ValueTask, no allocation
```

Wire it into `BulkEmbeddingOptions.TokenEstimator`:

```csharp
var counter = new TiktokenCounter("gpt-4o");
var options = new BulkEmbeddingOptions
{
    MaxBatchTokens = 8000,
    TokenEstimator = counter.ToTokenEstimator() // replaces s.Length / 4
};
```

`ToTokenEstimator()` hangs off `TiktokenCounter` (not `ITokenCounter`) — remote async counters
cannot be accidentally used in the synchronous batching loop.

`TiktokenCounter` also exposes `GetIndexByTokenCount(text, maxTokenCount)` and
`GetIndexByTokenCountFromEnd(text, maxTokenCount)` for O(n) single-pass token-boundary
slicing. Use `ToTokenSlicerFromStart()` / `ToTokenSlicerFromEnd()` to get `Func<string, int, int>`
delegates for `RecursiveChunkerOptions.TokenSlicerFromStart` / `TokenSlicerFromEnd`.

**Cohere counter (`CohereTokenCounter` in `Cisharpai.Cohere`):** calls `POST /v1/tokenize`.

```csharp
using Cisharpai.Cohere;

var counter = CohereTokenCounter.Create(handlerFactory, options, "embed-english-v3.0");
int tokens = await counter.CountAsync("Hello, world!");
```

Text over 65,536 characters is split on whitespace boundaries and summed — this is an
**upper-bound approximation** (BPE merges across the split point are lost).

**Testing (`FakeTokenCounter` in `Cisharpai.Testing`):**

```csharp
var fake = new FakeTokenCounter { DefaultCount = 10 };
fake.EnqueueCount(42); // first call returns 42, then falls back to DefaultCount
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
| Grounded Chat | `IGroundedChatFeature` | OpenAI (GPT-5, native), Azure OpenAI (GPT-5, native), Azure AI Inference (synthesized fallback), Anthropic (native), Cohere (native) |
| Web Search | `IWebSearchFeature` | Anthropic (server-side `web_search` tool), OpenAI (GPT-5 only, Responses API `web_search` tool) |
| Hosted Retrieval | `IHostedRetrievalFeature` (in `Cisharpai.Rag`) | OpenAI (GPT-5 only, Responses API `file_search` tool) |
| Prompt Caching | `IPromptCachingFeature` | Anthropic only (control); OpenAI/Azure report cache usage on the unified response without this interface |
| Image Embedding | `IImageEmbeddingFeature` | Azure AI Inference, Cohere |
| Multimodal Embedding | `IMultimodalEmbeddingFeature` | Cohere only |

Reranking is not a feature interface — it is its own top-level client (`IRerankerClient`),
implemented by Cohere only.
## RAG (`Cisharpai.Rag`)

Use the separate `Cisharpai.Rag` package for ingestion, retrieval, and context packing. Ingestion and the built-in `InMemoryRetriever` require an `IEmbeddingClient`; other `IRetriever` implementations use their own backends (search engines, hosted vector stores, SQL) and do not depend on `IEmbeddingClient`. This is independent of `IGroundedChatFeature`; it does not provide storage drivers or generation.

- Root namespace `Cisharpai.Rag`: `IRagIngestionPipeline`, `RagIngestionPipeline`, `RagOptions`, `AddCisharpaiRag`.
- `.Chunking`: `ITextChunker.ChunkAsync(RagDocument, CancellationToken)` returns `IAsyncEnumerable<TextChunk>`, `FixedSizeChunker`, `FixedSizeChunkerOptions`. `SemanticChunker(IBulkEmbeddingProcessor, SemanticChunkerOptions?, ISentenceSplitter?)` — similarity-based chunking; `SemanticThresholdStrategy.Percentile` (default, self-calibrating) or `Absolute`; both modes buffer all sentence embeddings in memory before emitting the first chunk; backstops via `MaxChunkCharacters` (default 8000) and `MaxChunkSentences` (default 50). `ISentenceSplitter` / `RegexSentenceSplitter` — pluggable sentence splitting (default targets English prose, will mis-split on abbreviations). **This chunker embeds the entire document at chunking time — costs money and latency on top of downstream embedding.** `RecursiveChunker(RecursiveChunkerOptions?)` — structure-aware recursive splitting with a configurable separator ladder (default: `["\n\n", "\n", ". ", " ", ""]` — paragraph, line, sentence, word, hard cut). Character-based sizing by default (Unicode scalar values, matching `FixedSizeChunker`); set `TokenCounter` to an `ITokenCounter` for token-based sizing. Token mode requires `TokenSlicerFromStart` for hard cuts and `TokenSlicerFromEnd` when `ChunkOverlap > 0` (the default) — use `TiktokenCounter.ToTokenSlicerFromStart()` / `ToTokenSlicerFromEnd()`. Token mode issues one counter call per candidate boundary, so a remote counter is impractical for large documents; `TiktokenCounter` (local) is strongly recommended. The implementation materialises all raw chunks before yielding. Options: `MaxChunkSize` (1024), `ChunkOverlap` (128, same unit as size), `Separators`. The empty-string terminal separator guarantees every chunk fits the budget; without it, oversized atomic units are emitted as-is.
- `.Embeddings`: `IBulkEmbeddingProcessor.EmbedAsync(chunks, progress?, ct)`, `BulkEmbeddingProcessor`, `BulkEmbeddingOptions`, `EmbeddingProviderProfile`.
- `.Models`: `RagDocument(Id, Text)`, `TextChunk(DocumentId, Index, StartOffset, EndOffset, Text, Metadata)`, `ChunkEmbedding(Chunk, Vector)`, `EmbeddingBatchResult`, `BulkEmbeddingProgress(CompletedBatches, TotalChunksProcessed, FailedBatches)`.
- `TextChunk.EndOffset` is the exclusive UTF-16 end offset (for verbatim chunks: `StartOffset + Text.Length`); stored rather than derived to support non-verbatim chunkers that prepend context. `Metadata` is `IReadOnlyDictionary<string, object?>`, always non-null, empty by default. `object?` values support numeric metadata (similarity scores) without stringification.
- Chunk defaults: 1024 Unicode scalar values and 128 overlap; require positive size and `0 <= overlap < size`. `StartOffset` and `EndOffset` use UTF-16 units; `EndOffset >= StartOffset` is validated. Preserve whitespace and valid surrogate pairs; empty text yields no chunks. Callers own document ID uniqueness.
- Embedding defaults: `MaxBatchItems = 32`, `MaxBatchTokens = null`, `MaxConcurrency = 1`, `MaxPendingBatches = null` (meaning `MaxConcurrency * 2`), `MaxRetries = 3`, `RetryBaseDelay = 1s`, `InputType = EmbeddingInputType.Document`, float encoding. Batch closes when either `MaxBatchItems` or `MaxBatchTokens` is reached. `TokenEstimator` (default `s => s.Length / 4`) provides the seam for Phase 2 tokenizers. Options include `Model`, `Dimensions`, `IncludeRawResponse`, `ExtraParameters`, `IsTransientError`.
- Per-provider batch ceilings: `BulkEmbeddingOptions.ForProvider(EmbeddingProviderProfile.OpenAi)` for new options, or `existingOptions.ApplyProfile(profile)` to overwrite just `MaxBatchItems`/`MaxBatchTokens` (returns the same instance, so apply it *before* explicit overrides). Profiles: `Conservative` 32/none, `OpenAi` 96/250000, `AzureOpenAi` 16/100000, `AzureAiInference` 64/100000, `Cohere` 96/100000. Conservative starting points, not authoritative provider caps — verify per deployment.
- `RetryBaseDelay` binds from standard `TimeSpan` strings (`"00:00:01"`); a bare `"1"` means one day.
- Direct composition: `new RagIngestionPipeline(new FixedSizeChunker(chunkOptions), new BulkEmbeddingProcessor(client, embeddingOptions))`.
- Register provider first, then `services.AddCisharpaiRag(o => { o.Chunking.ChunkSize = 1024; o.Embedding.MaxBatchItems = 96; o.Embedding.MaxBatchTokens = 8000; })`. Host configuration binding is the application's responsibility: `configuration.GetSection("Rag").Bind(o)` inside the callback. `TokenEstimator` and `IsTransientError` are `Func<>` delegates — set them in code after `Bind`.
- Keyed selection: `services.AddCisharpaiRag(sp => sp.GetRequiredKeyedService<IEmbeddingClient>("documents"), o => o.Embedding.Model = "text-embedding-3-small")`. Processors/pipelines are scoped; resolve within a service scope. Options are validated and snapshotted at construction/resolution; no live reload.
- `IngestAsync` and `EmbedAsync` accept collections or async streams, an optional `IProgress<BulkEmbeddingProgress>`, and a cancellation token. With `MaxConcurrency = 1`, processing is sequential. With higher values, batches run in parallel but results are yielded in input order; `MaxPendingBatches` (>= `MaxConcurrency`) bounds how many dispatched batches may wait for in-order delivery, so read-ahead and memory never grow with corpus size.
- Check `batch.IsSuccess` before reading `batch.Items`; each item has `Chunk` and `Vector`. `batch.Chunks`, zero-based `BatchIndex` and `Response` retain input identity and provider metadata/raw payloads. Failed batches (provider error, malformed response, exhausted retries) have empty items but do NOT stop the run — processing continues. Transient failures are retried with exponential backoff and jitter; `DefaultIsTransient` matches a standalone `429` or any `500`-`599` status in the error message plus the usual throttling/server-error phrases, and `IsTransientError` overrides it. No rollback/checkpoints. Provider HTTP resilience remains independent.
- Cancellation and network/configuration exceptions propagate. Fake with existing `FakeEmbeddingClient`, queuing one float vector per expected chunk; a default single-vector response fails validation for multi-chunk batches.
- `.Packing`: `IContextPacker.PackAsync(rankedChunks, options, ct)`, `ContextPacker` (constructor-injects `ITokenCounter`). Input: `IReadOnlyList<ScoredChunk>` where `ScoredChunk(TextChunk, double Score)`. Output: `ContextPackingResult(Selected, Dropped, TotalTokensUsed, BudgetRemaining)`. `DroppedChunk(ScoredChunk, TokenCount, DropReason)` with `DropReason.BudgetExhausted` or `IndividuallyOversized`. Per-call `ContextPackingOptions`: `TokenBudget` (positive), `ReservedTokens` (non-negative, < budget), `Separator` ("\n\n" default, null rejected), `UseLostInMiddleOrdering` (true default — strongest chunks at context edges), `OverflowStrategy` (SkipAndContinue default or StopAtFirstMisfit). Budget accounting: `TokenBudget - ReservedTokens - separators(n-1) - chunkTokens`. Each chunk counted once via the injected `ITokenCounter` and cached for the call. Individually oversized chunks are always skipped (even in StopAtFirstMisfit). Anthropic `count_tokens` is deferred — it is message-shaped and must not be called inside the packing loop.
- **Retrieval**: `IRetriever.RetrieveAsync(string query, int topK, CancellationToken)` returns `Task<IReadOnlyList<ScoredChunk>>`. Backend-agnostic — no vector vocabulary in the contract. The query is a `string`, not a vector; no filter parameter. Implementations may use dense embeddings, BM25/lexical search, hybrid fusion, SQL, or hosted stores. `InMemoryRetriever(IEmbeddingClient, model?)` is a brute-force cosine-similarity demo/testing aid — load via `Add(TextChunk, float[])` / `AddRange(...)`, not for production. Returns empty on embedding failure instead of throwing.
- **Rank Fusion**: `RankFusion.ReciprocalRank(IReadOnlyList<IReadOnlyList<ScoredChunk>>, k=60)` merges multiple ranked lists into one via `1/(k+rank)` scoring (rank is one-based). Enables hybrid retrieval without the library implementing either search strategy. Items in only one list receive their single-list score. Items are identified by `(DocumentId, Index)`.
- **Testing**: `FakeRetriever` in `Cisharpai.Testing` — queue/default/capture pattern like the other fakes. `FakeResponses.Retriever()` (empty default) or `FakeResponses.Retriever(scoredChunks)`. DI: `services.AddFakeRetriever()`.
- **Hosted Retrieval**: `IHostedRetrievalFeature` (in `Cisharpai.Rag`) is a factory that returns an `IRetriever` bound to a specific provider-hosted vector store. Discover via `client.Features.Get<IHostedRetrievalFeature>()?.ForStore(vectorStoreId)`. Currently implemented by OpenAI only (GPT-5 models via Responses API `file_search` tool). The returned `IRetriever` maps `file_search` results to `ScoredChunk` with passage-relative `TextChunk` offsets (`StartOffset=0`, `EndOffset=text.Length`). Failed `file_search_call` items set log warnings and are excluded from results; HTTP/API errors return empty list (no throw). `DefaultModel` must be set on `OpenAiClientOptions`. `OpenAiVectorStoreClient` provides store/file lifecycle management (create, upload, poll-until-processed with bounded timeout/cancellation, list, delete) via `VectorStoreResult<T>` (IsSuccess/Value/ErrorMessage). DI: `services.AddOpenAiVectorStoreClient(o => { ... })`.
- **Testing Hosted Retrieval**: `FakeHostedRetrievalFeature` in `Cisharpai.Testing` — per-store `FakeRetriever` dictionary. `AddStore(storeId)` registers a store, `ForStore(storeId)` returns its `FakeRetriever`, `GetRetriever(storeId)` accesses the fake for setup (enqueue responses, check captured queries). Unregistered store IDs return an empty-default `FakeRetriever` (no throw).
- **Evaluation** (`.Evaluation`): LLM-as-judge scorers and pure ranking metrics for measuring RAG quality.
  - `IRagEvaluator` — contract: `EvaluateAsync(question, answer, contexts, ct)` returns `Task<EvaluationScore>`. Provider-agnostic — any `IChatCompletionClient` with `IJsonOutputFeature`.
  - `EvaluationScore(double Score, string Rationale)` — immutable record, `Score` in `[0, 1]`, validated.
  - Four built-in evaluators (all extend `JudgeEvaluatorBase`):
    - `GroundednessEvaluator` — is the answer supported by the context?
    - `AnswerRelevanceEvaluator` — does the answer address the question?
    - `ContextPrecisionEvaluator` — are the retrieved chunks relevant to the question?
    - `ContextRecallEvaluator` — do the chunks cover the information needed to answer?
  - Evaluators use system/user message separation with untrusted-data guards to mitigate prompt injection from user-provided inputs.
  - `RankingMetrics` — pure-function static class, no model calls:
    - `Ndcg(retrievedIds, relevantIds)` — Normalized Discounted Cumulative Gain
    - `Mrr(retrievedIds, relevantIds)` — Mean Reciprocal Rank
    - `RecallAtK(retrievedIds, relevantIds, k)` — Recall at k
  - All ranking metrics accept `IReadOnlySet<string>` or `IReadOnlyList<string>` for `relevantIds`. Duplicate retrieved IDs are deduplicated (each relevant ID credited once).
  - Test with `FakeChatCompletionClient` — enqueue JSON responses with `EnqueueJsonOutputResponse(...)`.

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
- [references/grounded-chat.md](references/grounded-chat.md) — RAG with citations (OpenAI, Azure OpenAI, Anthropic, Cohere)
- [references/web-search.md](references/web-search.md) — Web search with citations (Anthropic, OpenAI GPT-5)
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
- `src/Cisharpai.Rag/` — Chunking, bulk embeddings, document ingestion, context packing, retrieval contract, rank fusion, and configuration
- `src/Cisharpai.Testing/` — Fake clients for unit testing
- `src/Cisharpai.Tests/` — Unit tests (all providers)
- `src/Cisharpai.Integration.Tests/` — Integration tests (.NET 10 only)

## Troubleshooting

**Response returns `IsSuccess = false`:**
Set `IncludeRawResponse = true` on the request and inspect `RawResponseJson`.

**Feature returns `null` from `Get<T>()`:**
The provider doesn't support that feature. Check [references/provider-features.md](references/provider-features.md).

**Build targets:** Projects multitarget .NET 8.0 and .NET 10.
