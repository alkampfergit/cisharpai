# RAG ingestion foundations

`Cisharpai.Rag` splits identified documents into fixed-size chunks and embeds them through any `IEmbeddingClient`. It targets .NET 8 and .NET 10. Install `Cisharpai.Rag` and an embedding provider package such as `Cisharpai.OpenAi`; use `Cisharpai.Testing` for offline tests.

The three components work independently: `ITextChunker` splits documents, `IBulkEmbeddingProcessor` embeds existing chunks, and `IRagIngestionPipeline` composes both. This increment provides ingestion only: no file parsing, vector database, retrieval, generation, tokenizer, or provider asynchronous batch jobs.

## Direct chunking

```csharp
using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Models;

var chunker = new FixedSizeChunker(new FixedSizeChunkerOptions
{
    ChunkSize = 4,
    Overlap = 1
});
var document = new RagDocument("manual/intro", "abcdefghi");
foreach (var chunk in chunker.Chunk(document))
{
    Console.WriteLine($"{chunk.DocumentId}:{chunk.Index} @ {chunk.StartOffset}: {chunk.Text}");
}
// manual/intro:0 @ 0: abcd
// manual/intro:1 @ 3: defg
// manual/intro:2 @ 6: ghi
```

Size and overlap count **Unicode scalar values**, not UTF-16 code units, grapheme clusters, bytes, or tokens. A supplementary character such as an emoji counts as one scalar; combining sequences can span chunks. `StartOffset` is a UTF-16 offset suitable for `document.Text.Substring(chunk.StartOffset, chunk.Text.Length)`. Valid surrogate pairs are never split. Text and whitespace are preserved; empty documents yield no chunks, and no redundant overlap-only final chunk is produced. IDs must be nonblank, text must be nonnull, and callers own ID uniqueness. Chunk indices start at zero for each document.

## Direct pipeline and bulk embedding

Given an already configured `IEmbeddingClient embeddingClient`:

```csharp
using Cisharpai;
using Cisharpai.Rag;
using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Embeddings;
using Cisharpai.Rag.Models;

var processor = new BulkEmbeddingProcessor(embeddingClient, new BulkEmbeddingOptions
{
    Model = "text-embedding-3-small",
    MaxBatchItems = 96,
    MaxBatchTokens = 8000,
    MaxConcurrency = 3,
    MaxRetries = 3,
    RetryBaseDelay = TimeSpan.FromSeconds(1)
});
var pipeline = new RagIngestionPipeline(new FixedSizeChunker(), processor);

await foreach (var batch in pipeline.IngestAsync(new[]
{
    new RagDocument("handbook", "Your source document text")
}))
{
    if (!batch.IsSuccess)
    {
        Console.Error.WriteLine($"Batch {batch.BatchIndex} failed: {batch.ErrorMessage}");
        continue; // failed batch does not stop the run
    }

    foreach (var item in batch.Items)
        Console.WriteLine($"{item.Chunk.DocumentId}:{item.Chunk.Index}: {item.Vector.Length} dimensions");
}
```

For pre-chunked input, call `processor.EmbedAsync(chunks, progress, cancellationToken)` with either `IEnumerable<TextChunk>` or `IAsyncEnumerable<TextChunk>`. No document pipeline is required. The processor preserves the input order and pairs each vector with the corresponding submitted chunk. It requests float vectors only.

## Batch sizing

A batch closes when **either** constraint is hit:

- **`MaxBatchItems`** (int, default 32) — maximum number of chunks per request. Set this based on your provider's per-request item limit (e.g. 96 for OpenAI, 96 for Cohere, 32 as conservative fallback).
- **`MaxBatchTokens`** (int?, default null) — maximum estimated tokens per request. When set, the batch closes when the next chunk would push the running estimate past the budget. The first chunk is always included regardless of budget.
- **`TokenEstimator`** (`Func<string, int>?`, default `s => s.Length / 4`) — token estimation function. The default uses a conservative character-based heuristic. Swap in a real tokenizer for more accurate batching.

Since `BulkEmbeddingProcessor` receives `IEmbeddingClient` without knowing the provider, the right ceilings must come from the caller or DI registration — not from sniffing the client type.

## Concurrency

**`MaxConcurrency`** (int, default 1) controls how many embedding requests run in parallel. With the default of 1, batches are processed sequentially. With higher values, multiple batches are dispatched concurrently through a bounded semaphore.

`BatchIndex` ordering is preserved regardless of completion order — results are always yielded in input order. Already-completed batches are never discarded on cancellation.

## Retry

**`MaxRetries`** (int, default 3) and **`RetryBaseDelay`** (TimeSpan, default 1s) control retry behavior. Transient failures (HTTP 429, 5xx) are retried with exponential backoff and jitter. Only the failed batch retries; other batches continue. After exhausting retries, the batch is surfaced as a failed `EmbeddingBatchResult` and processing continues to the next batch.

Override the default transient detection with **`IsTransientError`** (`Func<EmbeddingResponse, bool>?`). The default heuristic (`BulkEmbeddingProcessor.DefaultIsTransient`) checks for `429`, `rate limit`, `too many requests`, `throttl*`, `500`–`504`, and common server error phrases in the error message.

Set `MaxRetries = 0` to disable retry entirely.

## Progress observability

Both `EmbedAsync` and `IngestAsync` accept an optional `IProgress<BulkEmbeddingProgress>` parameter:

```csharp
var progress = new Progress<BulkEmbeddingProgress>(p =>
    Console.WriteLine($"Batches: {p.CompletedBatches}, Chunks: {p.TotalChunksProcessed}, Failed: {p.FailedBatches}"));

await foreach (var batch in pipeline.IngestAsync(documents, progress, cancellationToken))
{
    // ...
}
```

`BulkEmbeddingProgress` reports `CompletedBatches`, `TotalChunksProcessed`, and `FailedBatches` after each batch completes.

## Host configuration

The consuming application owns configuration binding. A console host can reference `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Configuration.Binder` in addition to the RAG/provider packages. Do not put API keys in the RAG configuration; supply them through your host's secret or environment configuration.

`appsettings.json`:

```json
{
  "Rag": {
    "Chunking": { "ChunkSize": 1024, "Overlap": 128 },
    "Embedding": {
      "MaxBatchItems": 96,
      "MaxBatchTokens": 8000,
      "MaxConcurrency": 3,
      "MaxRetries": 3,
      "Model": "text-embedding-3-small",
      "InputType": "Document",
      "IncludeRawResponse": false
    }
  }
}
```

```csharp
using Cisharpai.OpenAi;
using Cisharpai.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOpenAiEmbeddingClient(options =>
{
    options.ApiKey = builder.Configuration["OpenAI:ApiKey"]
        ?? throw new InvalidOperationException("Configure OpenAI:ApiKey.");
});
builder.Services.AddCisharpaiRag(options =>
{
    builder.Configuration.GetSection("Rag").Bind(options);
    // Code-only overrides applied after binding:
    options.Embedding.TokenEstimator = text => text.Length / 4;
});

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var pipeline = scope.ServiceProvider.GetRequiredService<IRagIngestionPipeline>();
```

For code-only DI configuration, omit `Bind` and assign properties in the callback. `AddCisharpaiRag()` uses all defaults and resolves the unkeyed `IEmbeddingClient`; register that client separately. Processors and pipelines are scoped, so resolve them inside a scope. Options are validated and snapshotted when components are constructed/resolved, before provider traffic; changes to the original options do not reconfigure existing components. Configuration reload is not automatic.

`TokenEstimator` and `IsTransientError` are `Func<>` delegates and cannot be bound from JSON. Set them in code after `Bind`. `RetryBaseDelay` binds from `"HH:MM:SS"` or `"SS"` format strings.

### Keyed providers

Use a factory when multiple embedding clients are registered. This replaces the unkeyed registration example above:

```csharp
using Cisharpai;
using Cisharpai.OpenAi;
using Cisharpai.Rag;
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddOpenAiEmbeddingClient("documents", options =>
{
    options.ApiKey = builder.Configuration["OpenAI:ApiKey"]
        ?? throw new InvalidOperationException("Configure OpenAI:ApiKey.");
});
builder.Services.AddCisharpaiRag(
    sp => sp.GetRequiredKeyedService<IEmbeddingClient>("documents"),
    options =>
    {
        options.Embedding.Model = "text-embedding-3-small";
        options.Chunking.ChunkSize = 768;
        options.Chunking.Overlap = 96;
    });
```

The factory runs in the scope resolving the processor. Choose Azure OpenAI, Azure AI Inference, or Cohere registration and the corresponding key without changing ingestion code; choose a model and dimensions supported by that provider. RAG options do not configure provider credentials, endpoints, deployments, or HTTP resilience. See [Embeddings](embeddings.md) for provider setup.

## Configuration reference

`RagOptions` groups `Chunking` (`FixedSizeChunkerOptions`) and `Embedding` (`BulkEmbeddingOptions`).

| Option | Default | Meaning / validation |
|---|---|---|
| `Chunking.ChunkSize` | `1024` | Positive number of Unicode scalar values per chunk |
| `Chunking.Overlap` | `128` | Scalar values shared by consecutive chunks; `0 <= Overlap < ChunkSize` |
| `Embedding.MaxBatchItems` | `32` | Positive maximum chunks per request; ≤ provider item limit |
| `Embedding.MaxBatchTokens` | `null` | Optional positive token budget per request; closes batch when exceeded |
| `Embedding.TokenEstimator` | `s => s.Length / 4` | Token estimation function; must not be null when `MaxBatchTokens` is set |
| `Embedding.MaxConcurrency` | `1` | Parallel requests; 1–32 |
| `Embedding.MaxRetries` | `3` | Retry count for transient failures; ≥ 0 |
| `Embedding.RetryBaseDelay` | `1s` | Base delay for exponential backoff; > 0 |
| `Embedding.IsTransientError` | `null` | Custom transient detection; uses `DefaultIsTransient` when null |
| `Embedding.Model` | `null` | Use provider default when unset; otherwise request this model |
| `Embedding.InputType` | `Document` | Unified embedding purpose hint, interpreted by the provider |
| `Embedding.Dimensions` | `null` | Optional positive requested vector length; provider must support it |
| `Embedding.IncludeRawResponse` | `false` | Request raw payload capture through the provider |
| `Embedding.ExtraParameters` | `null` | Optional `JsonElement` deep-merged into provider requests |

Lowering `ChunkSize` below the default overlap requires lowering `Overlap` as well. Token budget batching uses the `TokenEstimator` function — the default `s.Length / 4` is a deliberately conservative character-based heuristic. Phase 2 will introduce a real tokenizer; the `TokenEstimator` seam allows a drop-in swap.

For provider-specific parameters, assign JSON in the configuration callback:

```csharp
using System.Text.Json;

using var extra = JsonDocument.Parse("""{"truncate":"NONE"}""");
var extraParameters = extra.RootElement.Clone(); // Clone before the deferred callback.
builder.Services.AddCisharpaiRag(options =>
{
    // Example for a provider supporting this field, such as Cohere.
    options.Embedding.ExtraParameters = extraParameters;
});
```

Only use fields supported by your provider. Avoid overriding input/order or float encoding through `ExtraParameters`, because the pipeline relies on them for chunk/vector associations.

## Large inputs, cancellation, and partial progress

Stream documents so the corpus is never materialized. Each document still contains its full text string; this is document streaming, not incremental chunking of a single `TextReader`.

```csharp
using System.Runtime.CompilerServices;
using Cisharpai.Rag.Models;

static async IAsyncEnumerable<RagDocument> ReadDocuments(
    IEnumerable<string> paths,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    foreach (var path in paths)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        yield return new RagDocument(path, text);
    }
}

var progress = new Progress<BulkEmbeddingProgress>(p =>
    Console.WriteLine($"Progress: {p.CompletedBatches} batches, {p.TotalChunksProcessed} chunks, {p.FailedBatches} failed"));

using var cancellation = new CancellationTokenSource();
try
{
    await foreach (var batch in pipeline.IngestAsync(
        ReadDocuments(Directory.EnumerateFiles("documents", "*.txt")),
        progress,
        cancellation.Token))
    {
        if (!batch.IsSuccess)
        {
            Console.Error.WriteLine($"Batch {batch.BatchIndex} failed: {batch.ErrorMessage}");
            // Failed batch does not stop the run. batch.Chunks identifies the failed input.
            continue;
        }

        foreach (var item in batch.Items)
        {
            // Persist item.Chunk and item.Vector to your application's store here.
            Console.WriteLine($"Embedded {item.Chunk.DocumentId}:{item.Chunk.Index}");
        }
    }
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.WriteLine("Ingestion canceled.");
}
```

Caller-provided chunks need a nonblank document ID, nonnull text, and nonnegative chunk index/source offset. Invalid chunk metadata raises an argument exception before that batch is submitted. Empty chunk text is allowed locally but may be rejected by the provider.

To embed a large existing chunk stream, use the same loop with `processor.EmbedAsync(chunkStream, progress, cancellation.Token)`. With `MaxConcurrency = 1` (default), the next batch is not requested until the consumer advances. With higher concurrency, multiple batches may be in flight. The library buffers one batch plus the current document; retaining batches in your application consumes additional memory. Batches can span documents. Breaking out of enumeration disposes the source; pass cancellation to interrupt pending requests.

`EmbeddingBatchResult` exposes zero-based `BatchIndex`, submitted `Chunks`, successful `Items`, and the provider `Response` with model, token usage and captured raw payloads. Check `IsSuccess` before using vectors. A failed batch (provider error, malformed response, or exhausted retries) yields an `EmbeddingBatchResult` with `IsSuccess = false` and empty `Items`, then processing continues to the next batch. Malformed output includes wrong vector count, empty/non-finite vectors, inconsistent vector lengths, or a mismatch with requested dimensions. Previously yielded successes remain available; there is no rollback or automatic resume/checkpoint store. Use document IDs and chunk indices to implement your own persistence and restart policy.

Cancellation propagates as `OperationCanceledException`; network/configuration and source-enumeration exceptions propagate to the caller. Transient failures (429, 5xx) are retried with exponential backoff up to `MaxRetries` times. Provider HTTP resilience may still retry requests according to its own configuration. Permanent validation failures are not retried.

## Offline tests

Reuse `FakeEmbeddingClient` with one vector per submitted chunk. See [Testing](testing.md#rag-ingestion-tests) for a complete example and test commands. No new core feature interface or RAG-specific fake provider is required.
