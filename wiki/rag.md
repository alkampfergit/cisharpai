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
    BatchSize = 32
});
var pipeline = new RagIngestionPipeline(new FixedSizeChunker(), processor);

await foreach (var batch in pipeline.IngestAsync(new[]
{
    new RagDocument("handbook", "Your source document text")
}))
{
    if (!batch.IsSuccess)
    {
        Console.Error.WriteLine(batch.ErrorMessage);
        break;
    }

    foreach (var item in batch.Items)
        Console.WriteLine($"{item.Chunk.DocumentId}:{item.Chunk.Index}: {item.Vector.Length} dimensions");
}
```

For pre-chunked input, call `processor.EmbedAsync(chunks, cancellationToken)` with either `IEnumerable<TextChunk>` or `IAsyncEnumerable<TextChunk>`. No document pipeline is required. The processor preserves the input order and pairs each vector with the corresponding submitted chunk. It requests float vectors only.

## Host configuration

The consuming application owns configuration binding. A console host can reference `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Configuration.Binder` in addition to the RAG/provider packages. Do not put API keys in the RAG configuration; supply them through your host's secret or environment configuration.

`appsettings.json`:

```json
{
  "Rag": {
    "Chunking": { "ChunkSize": 1024, "Overlap": 128 },
    "Embedding": {
      "BatchSize": 32,
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
    // Optional code overrides are applied after binding.
    options.Embedding.BatchSize = 16;
});

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var pipeline = scope.ServiceProvider.GetRequiredService<IRagIngestionPipeline>();
```

For code-only DI configuration, omit `Bind` and assign `options.Chunking.ChunkSize`, `options.Chunking.Overlap`, and `options.Embedding` properties in the callback. `AddCisharpaiRag()` uses all defaults and resolves the unkeyed `IEmbeddingClient`; register that client separately. Processors and pipelines are scoped, so resolve them inside a scope. Options are validated and snapshotted when components are constructed/resolved, before provider traffic; changes to the original options do not reconfigure existing components. Configuration reload is not automatic.

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
| `Embedding.BatchSize` | `32` | Positive maximum number of chunks in one request |
| `Embedding.Model` | `null` | Use provider default when unset; otherwise request this model |
| `Embedding.InputType` | `Document` | Unified embedding purpose hint, interpreted by the provider |
| `Embedding.Dimensions` | `null` | Optional positive requested vector length; provider must support it |
| `Embedding.IncludeRawResponse` | `false` | Request raw payload capture through the provider |
| `Embedding.ExtraParameters` | `null` | Optional `JsonElement` deep-merged into provider requests |

Lowering `ChunkSize` below the default overlap requires lowering `Overlap` as well. Chunk sizes do not enforce provider token limits, and batch size does not enforce total request token/byte limits. Tune both for your model and input language.

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

using var cancellation = new CancellationTokenSource();
try
{
    await foreach (var batch in pipeline.IngestAsync(
        ReadDocuments(Directory.EnumerateFiles("documents", "*.txt")),
        cancellation.Token))
    {
        if (!batch.IsSuccess)
        {
            Console.Error.WriteLine($"Batch {batch.BatchIndex} failed: {batch.ErrorMessage}");
            // batch.Chunks identifies the failed input. batch.Items is empty.
            break;
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

To embed a large existing chunk stream, use the same loop with `processor.EmbedAsync(chunkStream, cancellation.Token)`. Requests are sequential, and the next batch is not requested until the consumer advances. The library buffers one batch plus the current document; retaining batches in your application consumes additional memory. Batches can span documents. Breaking out of enumeration disposes the source; pass cancellation to interrupt a pending request.

`EmbeddingBatchResult` exposes zero-based `BatchIndex`, submitted `Chunks`, successful `Items`, and the provider `Response` with model, token usage and captured raw payloads. Check `IsSuccess` before using vectors. A provider error or malformed success yields one failed batch with no items, then stops. Malformed output includes wrong vector count, empty/non-finite vectors, inconsistent vector lengths, or a mismatch with requested dimensions. Previously yielded successes remain available; there is no rollback or automatic resume/checkpoint store. Use document IDs and chunk indices to implement your own persistence and restart policy.

Cancellation propagates as `OperationCanceledException`; network/configuration and source-enumeration exceptions propagate to the caller. There are no ingestion-level retries. Provider HTTP resilience may still retry requests according to its own configuration. Avoid repeatedly retrying permanent validation failures.

## Offline tests

Reuse `FakeEmbeddingClient` with one vector per submitted chunk. See [Testing](testing.md#rag-ingestion-tests) for a complete example and test commands. No new core feature interface or RAG-specific fake provider is required.
