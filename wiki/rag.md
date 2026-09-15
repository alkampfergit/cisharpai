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
await foreach (var chunk in chunker.ChunkAsync(document))
{
    Console.WriteLine($"{chunk.DocumentId}:{chunk.Index} @ {chunk.StartOffset}–{chunk.EndOffset}: {chunk.Text}");
}
// manual/intro:0 @ 0–4: abcd
// manual/intro:1 @ 3–7: defg
// manual/intro:2 @ 6–9: ghi
```

`ITextChunker.ChunkAsync` returns `IAsyncEnumerable<TextChunk>` to support chunkers that need network I/O (e.g. semantic chunking via an embedding client). `FixedSizeChunker` is synchronous internally but exposes the async-streaming shape.

Size and overlap count **Unicode scalar values**, not UTF-16 code units, grapheme clusters, bytes, or tokens. A supplementary character such as an emoji counts as one scalar; combining sequences can span chunks. For locally-chunked text, `StartOffset` and `EndOffset` are zero-based UTF-16 offsets that delimit the original source span: `document.Text.Substring(chunk.StartOffset, chunk.EndOffset - chunk.StartOffset)` recovers the source slice for any chunker. **For hosted retrieval** (e.g. OpenAI `file_search`), offsets are passage-relative — `StartOffset` is `0` and `EndOffset` is `Text.Length` — because the provider chunked the file and returns a passage whose position in the original document is unknown. Do not assume offsets from different retrieval sources are comparable. For **verbatim** chunkers (all built-in chunkers), `Text` equals that source slice and `EndOffset == StartOffset + Text.Length`; callers may use `document.Text.Substring(chunk.StartOffset, chunk.Text.Length)` in that case. For **non-verbatim** chunkers (e.g. contextual retrieval that prefixes generated text), `Text` may differ from the source span and `Text.Length` does not equal the source span length — use `EndOffset - StartOffset` for the source range. `Metadata` is an `IReadOnlyDictionary<string, object?>` carrying chunker-specific data (e.g. boundary type, similarity score); it is defensively copied on construction, defaults to empty, and is never null. Valid surrogate pairs are never split. Built-in (verbatim) chunkers preserve text and whitespace exactly; non-verbatim chunkers may transform chunk text while preserving source offsets. Empty documents yield no chunks, and no redundant overlap-only final chunk is produced. IDs must be nonblank, text must be nonnull, and callers own ID uniqueness. Chunk indices start at zero for each document.

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

For pre-chunked input, call `processor.EmbedAsync(chunks, progress, cancellationToken)` with either `IEnumerable<TextChunk>` or `IAsyncEnumerable<TextChunk>`. No document pipeline is required. The processor preserves the input order and pairs each vector with the corresponding submitted chunk. It requests float vectors only. Pre-chunked `TextChunk` values must include a valid `EndOffset` (≥ `StartOffset`).

## Batch sizing

A batch closes when **either** constraint is hit:

- **`MaxBatchItems`** (int, default 32) — maximum number of chunks per request.
- **`MaxBatchTokens`** (int?, default null) — maximum estimated tokens per request. When set, the batch closes when the next chunk would push the running estimate past the budget. The first chunk is always included regardless of budget.
- **`TokenEstimator`** (`Func<string, int>?`, default `s => s.Length / 4`) — token estimation function. The default uses a conservative character-based heuristic. For accurate batching, swap in a real tokenizer via `TiktokenCounter.ToTokenEstimator()` (see [Token counting](#token-counting)).

### Per-provider presets

`BulkEmbeddingProcessor` receives an `IEmbeddingClient` without knowing which provider is behind it, so it cannot pick ceilings itself. Select them with a profile instead of sniffing the client type:

```csharp
// New options preconfigured for a provider:
var options = BulkEmbeddingOptions.ForProvider(EmbeddingProviderProfile.Cohere);

// Or apply a profile to options you already have (e.g. after configuration binding):
options.ApplyProfile(EmbeddingProviderProfile.AzureOpenAi);
```

| `EmbeddingProviderProfile` | `MaxBatchItems` | `MaxBatchTokens` |
|---|---|---|
| `Conservative` (matches the defaults) | `32` | `null` |
| `OpenAi` | `96` | `250000` |
| `AzureOpenAi` | `16` | `100000` |
| `AzureAiInference` | `64` | `100000` |
| `Cohere` | `96` | `100000` |

`ApplyProfile` overwrites **only** `MaxBatchItems` and `MaxBatchTokens` and returns the same instance, so apply it *before* any explicit batch-sizing override — otherwise the profile replaces it. Every other option is left untouched.

These values are conservative starting points chosen to stay inside each provider's documented per-request ceilings; they are not authoritative provider limits. Deployments, models and quotas vary — verify them for yours and override the two properties when they differ.

## Token counting

`ITokenCounter` provides real token counts via `CountAsync(string text)`. Each instance is constructed for one model — the model is baked in, not passed per call.

### Local tokenizer (TiktokenCounter)

`TiktokenCounter` in the separate `Cisharpai.Rag.Tokenizers` package uses `Microsoft.ML.Tokenizers` for offline, synchronous counting. It supports OpenAI-compatible tokenizers only: `gpt-4o` (o200k_base), `gpt-4` / `gpt-3.5-turbo` (cl100k_base). Install `Cisharpai.Rag.Tokenizers` to use it — it is a separate package so that consumers of `Cisharpai.Rag` who only need bulk embeddings do not pull in the multi-megabyte tokenizer data files.

```csharp
using Cisharpai.Rag.Tokenization;

var counter = new TiktokenCounter("gpt-4o");
int tokens = counter.CountTokens("Hello, world!"); // synchronous
int tokensAsync = await counter.CountAsync("Hello, world!"); // ValueTask — completes synchronously
```

Wire it into `BulkEmbeddingOptions.TokenEstimator` with the `ToTokenEstimator()` extension:

```csharp
using Cisharpai.Rag.Tokenization;
using Cisharpai.Rag.Embeddings;

var counter = new TiktokenCounter("gpt-4o");
var options = new BulkEmbeddingOptions
{
    MaxBatchTokens = 8000,
    TokenEstimator = counter.ToTokenEstimator() // replaces the s.Length / 4 heuristic
};
```

`ToTokenEstimator()` is intentionally defined on `TiktokenCounter` (not `ITokenCounter`) so that remote async counters cannot be accidentally used in the synchronous batching loop.

### Cohere tokenizer (CohereTokenCounter)

`CohereTokenCounter` in `Cisharpai.Cohere` calls Cohere's `POST /v1/tokenize` endpoint. Construct it for one model:

```csharp
using Cisharpai.Cohere;

var counter = CohereTokenCounter.Create(handlerFactory, options, "embed-english-v3.0");
int tokens = await counter.CountAsync("Hello, world!");
```

Or via DI:

```csharp
services.AddCohereTokenCounter("embed-english-v3.0", options =>
{
    options.ApiKey = "...";
});
```

The Cohere tokenize API accepts text of 1–65,536 characters per request. Text longer than 65,536 characters is split on whitespace boundaries and the per-chunk token counts are summed. This sum is an **upper-bound approximation**: BPE merges that would span the split point are lost, so the true count may be lower by a small number of tokens.

### FakeTokenCounter

For unit testing, use `FakeTokenCounter` in `Cisharpai.Testing`:

```csharp
var fake = new FakeTokenCounter { DefaultCount = 10 };
fake.EnqueueCount(42); // first call returns 42, subsequent calls return 10

// Or via DI:
services.AddFakeTokenCounter(defaultCount: 10);

// Or via FakeResponses:
var fake = FakeResponses.TokenCounter(defaultCount: 25);
```

## Concurrency

**`MaxConcurrency`** (int, default 1) controls how many embedding requests run in parallel. With the default of 1, batches are processed sequentially. With higher values, multiple batches are dispatched concurrently through a bounded semaphore.

`BatchIndex` ordering is preserved regardless of completion order — results are always yielded in input order. Already-completed batches are never discarded on cancellation.

Out-of-order completion means a finished batch may have to wait for an earlier, slower one before it can be handed to the caller. **`MaxPendingBatches`** (int?, default `MaxConcurrency * 2`) bounds how many dispatched batches may be waiting: the producer reserves a slot before dispatching and the consumer releases it only once that batch has been delivered. Input is therefore never read further ahead than the caller can consume, and memory does not grow with corpus size no matter how slow the head batch or the consumer is. Raise it to absorb more completion-order jitter at the cost of buffered vectors; it must be at least `MaxConcurrency`. It is ignored when `MaxConcurrency` is 1, which never reorders.

## Retry

**`MaxRetries`** (int, default 3) and **`RetryBaseDelay`** (TimeSpan, default 1s) control retry behavior. Transient failures (HTTP 429, 5xx) are retried with exponential backoff and jitter. Only the failed batch retries; other batches continue. After exhausting retries, the batch is surfaced as a failed `EmbeddingBatchResult` and processing continues to the next batch.

Override the default transient detection with **`IsTransientError`** (`Func<EmbeddingResponse, bool>?`). The default heuristic (`BulkEmbeddingProcessor.DefaultIsTransient`) scans the error message for a standalone `429` or any status in the full `500`–`599` range, plus the phrases `rate limit`, `too many requests`, `throttl*`, `internal server error`, `service unavailable`, `bad gateway` and `gateway timeout`. Digit runs that are not exactly three digits, or that are glued to a letter (a model name such as `embed-500d`), are ignored. Supply `IsTransientError` when your provider reports transient conditions differently.

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
      "MaxPendingBatches": 6,
      "MaxRetries": 3,
      "RetryBaseDelay": "00:00:01",
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
using Cisharpai.Rag.Embeddings;
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
    // Provider preset first, so bound configuration can still override the batch ceilings.
    options.Embedding.ApplyProfile(EmbeddingProviderProfile.OpenAi);
    builder.Configuration.GetSection("Rag").Bind(options);
    // Code-only overrides applied after binding:
    options.Embedding.TokenEstimator = text => text.Length / 4;
});

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var pipeline = scope.ServiceProvider.GetRequiredService<IRagIngestionPipeline>();
```

For code-only DI configuration, omit `Bind` and assign properties in the callback. `AddCisharpaiRag()` uses all defaults and resolves the unkeyed `IEmbeddingClient`; register that client separately. Processors and pipelines are scoped, so resolve them inside a scope. Options are validated and snapshotted when components are constructed/resolved, before provider traffic; changes to the original options do not reconfigure existing components. Configuration reload is not automatic.

`TokenEstimator` and `IsTransientError` are `Func<>` delegates and cannot be bound from JSON. Set them in code after `Bind`. `RetryBaseDelay` binds from standard `TimeSpan` strings such as `"00:00:01"` (one second) or `"00:00:00.250"` (250 ms). Do not use a bare number: `"1"` parses as **one day**, not one second.

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
| `Embedding.MaxPendingBatches` | `null` | Dispatched batches awaiting in-order delivery; positive and ≥ `MaxConcurrency`. Defaults to `MaxConcurrency * 2` |
| `Embedding.MaxRetries` | `3` | Retry count for transient failures; ≥ 0 |
| `Embedding.RetryBaseDelay` | `1s` | Base delay for exponential backoff; > 0 |
| `Embedding.IsTransientError` | `null` | Custom transient detection; uses `DefaultIsTransient` when null |
| `Embedding.Model` | `null` | Use provider default when unset; otherwise request this model |
| `Embedding.InputType` | `Document` | Unified embedding purpose hint, interpreted by the provider |
| `Embedding.Dimensions` | `null` | Optional positive requested vector length; provider must support it |
| `Embedding.IncludeRawResponse` | `false` | Request raw payload capture through the provider |
| `Embedding.ExtraParameters` | `null` | Optional `JsonElement` deep-merged into provider requests |

Lowering `ChunkSize` below the default overlap requires lowering `Overlap` as well. Token budget batching uses the `TokenEstimator` function — the default `s.Length / 4` is a deliberately conservative character-based heuristic. For real counts, use `counter.ToTokenEstimator()` on a `TiktokenCounter` instance (see [Token counting](#token-counting)).

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

Caller-provided chunks need a nonblank document ID, nonnull text, nonnegative chunk index/source offset, and `EndOffset >= StartOffset`. Invalid chunk metadata raises an argument exception before that batch is submitted. Empty chunk text is allowed locally but may be rejected by the provider.

To embed a large existing chunk stream, use the same loop with `processor.EmbedAsync(chunkStream, progress, cancellation.Token)`. With `MaxConcurrency = 1` (default), the next batch is not requested until the consumer advances. With higher concurrency, multiple batches may be in flight. The library buffers one batch plus the current document; retaining batches in your application consumes additional memory. Batches can span documents. Breaking out of enumeration disposes the source; pass cancellation to interrupt pending requests.

`EmbeddingBatchResult` exposes zero-based `BatchIndex`, submitted `Chunks`, successful `Items`, and the provider `Response` with model, token usage and captured raw payloads. Check `IsSuccess` before using vectors. A failed batch (provider error, malformed response, or exhausted retries) yields an `EmbeddingBatchResult` with `IsSuccess = false` and empty `Items`, then processing continues to the next batch. Malformed output includes wrong vector count, empty/non-finite vectors, inconsistent vector lengths, or a mismatch with requested dimensions. Previously yielded successes remain available; there is no rollback or automatic resume/checkpoint store. Use document IDs and chunk indices to implement your own persistence and restart policy.

Cancellation propagates as `OperationCanceledException`; network/configuration and source-enumeration exceptions propagate to the caller. Transient failures (429, 5xx) are retried with exponential backoff up to `MaxRetries` times. Provider HTTP resilience may still retry requests according to its own configuration. Permanent validation failures are not retried.

## Vector math helpers

`VectorMath` is a static class with brute-force scoring operations for in-memory retrieval. It avoids external dependencies and operates on `ReadOnlySpan<float>` for the hot path, with `float[]` and `IReadOnlyList<float>` convenience overloads.

```csharp
using Cisharpai.Rag;

float[] query = embeddingResponse.Embeddings[0];
float[][] corpus = loadedVectors; // from your store

// Cosine similarity (−1 to 1)
float score = VectorMath.CosineSimilarity(query, corpus[0]);

// Dot product
float dot = VectorMath.DotProduct(query, corpus[0]);

// Normalize to unit length (returns new array; input unchanged)
float[] unit = VectorMath.Normalize(query);

// Normalize in place (mutates the span/array)
VectorMath.NormalizeInPlace(corpus[0]);

// Top-k retrieval — returns (Index, Score) pairs in descending order
var topResults = VectorMath.TopK(query, corpus, k: 5);
foreach (var (index, similarity) in topResults)
    Console.WriteLine($"Candidate {index}: {similarity:F4}");
```

**Dimension mismatch** between two vectors throws `ArgumentException` — this is always a programming error (wrong model, mixed embedding runs).

**Zero-vector normalization** returns the zero vector unchanged. A zero-magnitude vector cannot be meaningfully normalized; returning it as-is prevents `NaN` from propagating into similarity scores.

**`Normalize` vs `NormalizeInPlace`**: `Normalize` takes `ReadOnlySpan<float>` and returns a new `float[]`, leaving the input untouched. `NormalizeInPlace` takes `Span<float>` and mutates it in place — use it at ingestion time to avoid per-vector allocations when normalizing a large corpus.

## Semantic chunking

> **Cost warning:** `SemanticChunker` embeds the entire document at chunking time — it costs money and latency on top of any downstream embedding. A user swapping `FixedSizeChunker` for this one should expect an additional embedding call covering every sentence in the document.

`SemanticChunker` places boundaries where the meaning shifts rather than where a counter runs out. It splits the document into sentences, embeds them via `IBulkEmbeddingProcessor`, and cuts where the cosine similarity between consecutive sentences drops.

```csharp
using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Embeddings;
using Cisharpai.Rag.Models;

var processor = new BulkEmbeddingProcessor(embeddingClient, new BulkEmbeddingOptions
{
    InputType = EmbeddingInputType.Document,
    MaxBatchItems = 96
});
var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
{
    Strategy = SemanticThresholdStrategy.Percentile,
    BreakPercentile = 10f,
    MaxChunkCharacters = 8000,
    MaxChunkSentences = 50
});

await foreach (var chunk in chunker.ChunkAsync(new RagDocument("handbook", text)))
{
    var preview = chunk.Text.Length <= 50 ? chunk.Text : chunk.Text[..50] + "…";
    Console.WriteLine($"{chunk.DocumentId}:{chunk.Index} @ {chunk.StartOffset}–{chunk.EndOffset}: {preview}");
}
```

### Threshold strategies

| Strategy | Default | Behaviour |
|---|---|---|
| `Percentile` (default) | `BreakPercentile = 10` | Bottom N-th percentile of similarity drops in *this* document become boundaries. Self-calibrating across models and domains. **Buffers all sentence embeddings in memory** before emitting the first chunk — memory is proportional to `sentences × embedding dimensions × 4 bytes`. |
| `Absolute` | `AbsoluteThreshold = 0.5` | Boundary when cosine similarity drops below a fixed threshold. The right number varies by embedding model and domain — tune per model. Buffers all sentence embeddings like Percentile mode. |

### Coverage

Emitted chunks cover the source document exactly — no gaps, no overlap. Separator text between sentences is attached to the preceding chunk, the first chunk is anchored at offset 0, and the last chunk extends to `document.Text.Length`, so leading and trailing text the sentence splitter did not claim (whitespace, or a fragment without terminal punctuation) is preserved rather than dropped. `string.Concat(chunks.Select(c => c.Text))` reconstructs the document verbatim.

The one exception is a document the splitter finds no sentences in at all — whitespace only, for example. That yields no chunks, because there is nothing retrievable in it.

### Backstops

Two backstops prevent any chunk from growing unbounded. Whichever limit trips first forces the cut:

- **`MaxChunkCharacters`** (default 8000) — hard size-based backstop preventing a chunk from exceeding the embedding model's input limit. A sentence count alone does not bound this — 50 sentences of legal prose can be tens of thousands of characters.
- **`MaxChunkSentences`** (default 50) — secondary guard limiting the number of sentences per chunk.

Both backstops measure the chunk that is actually **emitted**, which is wider than the sentences it contains: it also carries the separator text up to the next sentence, plus any leading text for the first chunk and trailing text for the last. If the chunk for a *single* sentence already exceeds `MaxChunkCharacters`, `SemanticChunker` throws an `InvalidOperationException` rather than emitting an over-budget chunk — raise the limit, or inject a sentence splitter that produces shorter segments.

### Sentence splitting

The default `RegexSentenceSplitter` splits on `.` `!` `?` followed by whitespace or end-of-string. It targets English prose and will mis-split on abbreviations like "Dr." and "e.g." — inject a custom `ISentenceSplitter` for domain-specific or multilingual segmentation:

```csharp
var chunker = new SemanticChunker(processor, options, sentenceSplitter: myCustomSplitter);
```

### Semantic chunker options reference

| Option | Default | Meaning |
|---|---|---|
| `Strategy` | `Percentile` | Threshold strategy: `Percentile` or `Absolute` |
| `BreakPercentile` | `10` | Bottom N-th percentile of drops → boundary (Percentile mode only); `(0, 100]` |
| `AbsoluteThreshold` | `0.5` | Cosine below this → boundary (Absolute mode only); `[-1, 1]` |
| `MaxChunkCharacters` | `8000` | Size-based backstop in characters; must be positive |
| `MaxChunkSentences` | `50` | Secondary backstop in sentences; must be positive |

## Context packing

`IContextPacker` selects and orders ranked chunks to fit within a token budget. The built-in `ContextPacker` constructor-injects an `ITokenCounter` (the counter is tied to a model, so it belongs on the instance). Each chunk is counted exactly once per `PackAsync` call and the count is cached for the duration of the operation.

```csharp
using Cisharpai.Rag.Packing;
using Cisharpai.Rag.Tokenization;

var counter = new TiktokenCounter("gpt-4o");
var packer = new ContextPacker(counter);

var result = await packer.PackAsync(rankedChunks, new ContextPackingOptions
{
    TokenBudget = 4096,
    ReservedTokens = 500,      // system prompt + expected completion
    Separator = "\n\n",
    UseLostInMiddleOrdering = true,
    OverflowStrategy = OverflowStrategy.SkipAndContinue
});

foreach (var chunk in result.Selected)
    Console.WriteLine($"Selected: {chunk.Chunk.DocumentId}:{chunk.Chunk.Index} (score {chunk.Score:F3})");

foreach (var drop in result.Dropped)
    Console.WriteLine($"Dropped: {drop.Chunk.Chunk.DocumentId}:{drop.Chunk.Chunk.Index} — {drop.Reason} ({drop.TokenCount} tokens)");

Console.WriteLine($"Tokens used: {result.TotalTokensUsed}, remaining: {result.BudgetRemaining}");
```

### Input

`IReadOnlyList<ScoredChunk>` — ranked chunks with relevance scores. `ScoredChunk(TextChunk Chunk, double Score)` wraps a `TextChunk` with a `double` score that accepts both `float` similarity scores from `VectorMath.TopK` and `double` reranker scores from `RerankResult.RelevanceScore` without precision loss.

### Selection

Greedy over the ranked list. Budget accounting: `TokenBudget - ReservedTokens - separator_tokens - chunk_tokens`. Separators are counted once (n-1 for n chunks). The first chunk has no separator cost.

**`OverflowStrategy.SkipAndContinue`** (default): skip a chunk that does not fit and continue packing lower-ranked chunks. Fills the budget more fully; the dropped-chunk report explains exactly what was skipped and why.

**`OverflowStrategy.StopAtFirstMisfit`**: stop at the first chunk that does not fit the remaining budget. Guarantees "everything above rank N is present". Individually oversized chunks (those that exceed the entire available budget) are still skipped rather than stopping, since they can never fit regardless of packing order.

### Ordering

**Lost-in-the-middle** (default, `UseLostInMiddleOrdering = true`): the highest-ranked chunks are placed at the start and end of the context, with the weakest in the middle. Models attend most reliably to context edges, so this improves answer quality. Set `UseLostInMiddleOrdering = false` to preserve rank order.

### Dropped chunk reporting

`ContextPackingResult.Dropped` reports every excluded chunk with its token count and reason:

- `DropReason.BudgetExhausted` — the remaining budget could not accommodate the chunk.
- `DropReason.IndividuallyOversized` — the chunk alone exceeds the entire available budget. The fix is to chunk smaller at the chunking stage.

`TotalTokensUsed` and `BudgetRemaining` are reported for debuggability.

### Options validation

Options are validated per call. Invalid values throw:

| Condition | Exception |
|---|---|
| `TokenBudget <= 0` | `ArgumentOutOfRangeException` |
| `ReservedTokens < 0` | `ArgumentOutOfRangeException` |
| `ReservedTokens >= TokenBudget` | `ArgumentOutOfRangeException` |
| `Separator` is null | `ArgumentNullException` |
| `OverflowStrategy` undefined enum value | `ArgumentOutOfRangeException` |

### Anthropic `count_tokens`

Deferred. `POST /v1/messages/count_tokens` is message-shaped (takes a full request payload), making it a poor fit for per-chunk counting but a good fit for whole-request verification. It is a network round-trip per call and must not be used inside the packing loop.

## Recursive chunking

`RecursiveChunker` splits text along structural boundaries — paragraphs, lines, sentences, words — before resorting to a hard character-level cut. This is the chunker most RAG applications should use: it respects natural language boundaries whenever possible while guaranteeing that every chunk fits within a configurable budget.

```csharp
using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Models;

var chunker = new RecursiveChunker(new RecursiveChunkerOptions
{
    MaxChunkSize = 512,
    ChunkOverlap = 64
});

await foreach (var chunk in chunker.ChunkAsync(new RagDocument("handbook", text)))
{
    Console.WriteLine($"{chunk.DocumentId}:{chunk.Index} @ {chunk.StartOffset}–{chunk.EndOffset}: " +
        (chunk.Text.Length <= 50 ? chunk.Text : chunk.Text[..50] + "…"));
}
```

The separator ladder tries each separator in order. The default is paragraph (`\n\n`), line (`\n`), sentence (`. `), word (` `), then hard character cut (`""`). When a piece exceeds the budget after splitting by one separator, the chunker falls through to the next. The empty-string terminal entry guarantees that every emitted chunk fits within `MaxChunkSize`.

### Character mode vs token mode

By default, sizes are measured in Unicode scalar values — a supplementary character (e.g. an emoji) counts as one, regardless of how many UTF-16 code units it occupies. This matches `FixedSizeChunker`. Set `TokenCounter` to measure in real tokens instead:

```csharp
using Cisharpai.Rag.Tokenization;

var counter = new TiktokenCounter("gpt-4o");
var chunker = new RecursiveChunker(new RecursiveChunkerOptions
{
    MaxChunkSize = 256,
    ChunkOverlap = 32,
    TokenCounter = counter,
    TokenSlicerFromStart = counter.ToTokenSlicerFromStart(),
    TokenSlicerFromEnd = counter.ToTokenSlicerFromEnd()
});
```

`TokenSlicerFromStart` and `TokenSlicerFromEnd` use `Microsoft.ML.Tokenizers`' O(n) single-pass `GetIndexByTokenCount` — no counting loop. They are required for the hard-cut terminal case in token mode (the chunker throws rather than silently falling back to a binary search over a potentially network-backed counter). `TokenSlicerFromEnd` is also required when `ChunkOverlap > 0` (the default is 128), since overlap computation in token mode needs it — set `ChunkOverlap = 0` if you want token mode without a from-end slicer. The `Cisharpai.Rag.Tokenizers` package is only needed when token-based sizing is used; character-based consumers never install it.

**Performance note:** token mode issues one `CountAsync` call per candidate split boundary during recursive splitting. With a remote counter (e.g. `CohereTokenCounter`), each call is an HTTP round-trip — impractical for large documents. `TiktokenCounter` (local, synchronous) is strongly recommended.

### Custom separators

Supply your own separator ladder for domain-specific splitting. For example, Markdown:

```csharp
var chunker = new RecursiveChunker(new RecursiveChunkerOptions
{
    MaxChunkSize = 1024,
    Separators = ["\n## ", "\n### ", "\n\n", "\n", ". ", " ", ""]
});
```

Include `""` as the final entry to guarantee max-size compliance.

### Recursive chunker options reference

| Option | Default | Meaning |
|---|---|---|
| `MaxChunkSize` | `1024` | Maximum chunk size in characters or tokens; must be positive |
| `ChunkOverlap` | `128` | Overlap between consecutive chunks in the same unit; `0 ≤ Overlap < MaxChunkSize` |
| `Separators` | `["\n\n", "\n", ". ", " ", ""]` | Ordered separator ladder; must not be empty |
| `TokenCounter` | `null` | Token counter for token-based sizing; `null` = character mode |
| `TokenSlicerFromStart` | `null` | `(text, maxTokens) → charIndex` for hard cuts; required in token mode |
| `TokenSlicerFromEnd` | `null` | `(text, maxTokens) → charIndex` for overlap; **required** in token mode when `ChunkOverlap > 0` (the default) |

## Retrieval

`IRetriever` is the backend-agnostic retrieval contract. Implementations may use dense embeddings, sparse/lexical search (BM25), hybrid fusion, SQL full-text, or a hosted provider store — all strategies are first-class.

```csharp
using Cisharpai.Rag;
using Cisharpai.Rag.Packing;

IRetriever retriever = /* your implementation */;
IReadOnlyList<ScoredChunk> results = await retriever.RetrieveAsync("What is the refund policy?", topK: 5);
```

The query parameter is a `string`, not a vector — implementations that need embeddings obtain them internally. The return type is the existing `ScoredChunk` record.

### InMemoryRetriever (demo/testing)

A brute-force cosine-similarity retriever for demos and tests. **Not suitable for production** — use a purpose-built vector store or search engine behind `IRetriever` instead.

```csharp
using Cisharpai.Rag;
using Cisharpai.Rag.Models;

var retriever = new InMemoryRetriever(embeddingClient, model: "text-embedding-3-small");

// Add pre-computed chunk/vector pairs (e.g. from BulkEmbeddingProcessor output)
retriever.Add(chunk, vector);
retriever.AddRange(chunkVectorPairs);

// Or feed BulkEmbeddingProcessor output directly — ChunkEmbedding is accepted natively
retriever.AddRange(batchResult.Items);

var results = await retriever.RetrieveAsync("search query", topK: 5);
```

### Hybrid retrieval with Reciprocal Rank Fusion

`RankFusion.ReciprocalRank` merges multiple ranked lists into one by summing `1 / (k + rank)` (where rank is one-based) across all lists in which an item appears. This enables hybrid retrieval — run a dense retriever and a BM25/lexical retriever independently, then fuse:

```csharp
using Cisharpai.Rag;

IReadOnlyList<ScoredChunk> denseResults = await denseRetriever.RetrieveAsync(query, topK: 20);
IReadOnlyList<ScoredChunk> lexicalResults = await bm25Retriever.RetrieveAsync(query, topK: 20);

var fused = RankFusion.ReciprocalRank(new[] { denseResults, lexicalResults });
// fused is sorted by descending RRF score
```

The library does not implement BM25 or any lexical search engine — that belongs in a purpose-built search engine. `RankFusion` makes a user's own BM25 retriever a first-class participant in hybrid pipelines.

### Implementing IRetriever with BM25

A BM25 retriever wraps your search engine behind the same `IRetriever` contract:

```csharp
public class Bm25Retriever : IRetriever
{
    private readonly ISearchEngine _engine;

    public Bm25Retriever(ISearchEngine engine) => _engine = engine;

    public async Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(
        string query, int topK, CancellationToken cancellationToken = default)
    {
        var hits = await _engine.SearchAsync(query, topK, cancellationToken);
        return hits.Select(h => new ScoredChunk(h.Chunk, h.Score)).ToList();
    }
}
```

### Hosted retrieval (OpenAI file_search)

`IHostedRetrievalFeature` is a factory that returns an `IRetriever` bound to a specific provider-hosted vector store. The provider manages chunking, embedding, and search — the caller supplies only a store identifier and a query.

```csharp
using Cisharpai.Rag;

var client = new OpenAiChatCompletionClient(httpClient, options);
var feature = client.Features.Get<IHostedRetrievalFeature>()!;

// Create a retriever for a specific vector store
IRetriever retriever = feature.ForStore("vs_my_store_id");
IReadOnlyList<ScoredChunk> results = await retriever.RetrieveAsync("What is the refund policy?", topK: 5);
```

Each `ForStore` call creates an independent retriever with no shared mutable state — two concurrent retrievals against different stores do not interfere. Consumers that only need retrieval depend on `IRetriever`, never on the hosted feature directly. DI registration for a single store:

```csharp
services.AddSingleton<IRetriever>(sp =>
    sp.GetRequiredService<IHostedRetrievalFeature>().ForStore(storeId));
```

**TextChunk mapping for hosted results:**

| Field | Value | Rationale |
|-------|-------|-----------|
| `DocumentId` | OpenAI file id | Truthful provenance |
| `Index` | `0` | Rank carried by list order per `IRetriever` contract |
| `StartOffset` | `0` | Passage-relative (provider-chunked, source offset unknown) |
| `EndOffset` | `Text.Length` | Passage-relative |
| `Metadata` | `file_id`, `filename`, provider attributes | Nothing lost |

**Error handling:** a failed `file_search_call` on the `IRetriever` path returns an empty list and logs the failure — it does not throw. On the chat path (`GetChatCompletionAsync`), a failed `file_search_call` sets `IsSuccess=false` with content preserved and `ErrorMessage` naming the failed call IDs, matching `IWebSearchFeature` semantics.

### Vector store management (OpenAI)

`OpenAiVectorStoreClient` wraps the OpenAI Vector Stores and Files APIs for store and file lifecycle management. This is a provider-specific client, not a generic storage abstraction.

```csharp
using Cisharpai.OpenAi;
using Cisharpai.OpenAi.Models;

var vsClient = OpenAiVectorStoreClient.Create(options);

// Create a store
var store = await vsClient.CreateStoreAsync(new OpenAiVectorStoreCreateRequest { Name = "My Docs" });

// Upload a file
using var stream = File.OpenRead("document.pdf");
var file = await vsClient.UploadFileAsync(stream, "document.pdf");

// Add the file to the store and poll until processed
await vsClient.AddFileToStoreAsync(store.Value!.Id, file.Value!.Id);
var processed = await vsClient.PollFileUntilProcessedAsync(
    store.Value.Id, file.Value.Id,
    timeout: TimeSpan.FromMinutes(5));

if (processed.Value?.Status == "failed")
    Console.WriteLine($"Processing failed: {processed.Value.LastError?.Message}");
```

**Scope boundary:** this client wraps provider-hosted file and store APIs. Local file management, document parsing, and storage abstractions over third-party stores are out of scope.

## Offline tests

Reuse `FakeEmbeddingClient` with one vector per submitted chunk, `FakeTokenCounter` for token counting (including with `ContextPacker`), `FakeRetriever` for retrieval, and `FakeHostedRetrievalFeature` for hosted retrieval. See [Testing](testing.md#rag-ingestion-tests) for a complete example and test commands.
