# Cisharpai.Rag

Provider-independent RAG ingestion foundations for .NET 8 and .NET 10: fixed-size text chunking, bulk float embeddings with token-aware batching, bounded concurrency and retry, and a composable document pipeline using `IEmbeddingClient`.

```csharp
using Cisharpai.Rag;
using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Embeddings;
using Cisharpai.Rag.Models;

// embeddingClient is an existing configured Cisharpai IEmbeddingClient.
var pipeline = new RagIngestionPipeline(
    new FixedSizeChunker(new FixedSizeChunkerOptions
    {
        ChunkSize = 1024,
        Overlap = 128
    }),
    new BulkEmbeddingProcessor(embeddingClient, new BulkEmbeddingOptions
    {
        // Provider presets set MaxBatchItems/MaxBatchTokens; apply before your own overrides.
        Model = "text-embedding-3-small",
        MaxConcurrency = 3
    }.ApplyProfile(EmbeddingProviderProfile.OpenAi)));

await foreach (var batch in pipeline.IngestAsync(new[]
{
    new RagDocument("guide", "Your document text")
}))
{
    if (!batch.IsSuccess)
    {
        Console.Error.WriteLine($"Batch {batch.BatchIndex} failed: {batch.ErrorMessage}");
        continue; // a failed batch does not stop the run
    }
    foreach (var item in batch.Items)
        Console.WriteLine($"{item.Chunk.DocumentId}:{item.Chunk.Index}: {item.Vector.Length}");
}
```

Register a provider separately, then use `services.AddCisharpaiRag(options => ...)` for DI. A factory overload selects keyed embedding clients. The same pipeline accepts `IAsyncEnumerable<RagDocument>` and cancellation; `BulkEmbeddingProcessor` also accepts existing chunk streams.

Defaults: chunk size 1024 Unicode scalar values, overlap 128, 32 items per batch with no token budget, sequential requests (`MaxConcurrency = 1`), 3 retries on transient failures, document input type, float vectors. `BulkEmbeddingOptions.ForProvider`/`ApplyProfile` set per-provider batch ceilings (`OpenAi`, `AzureOpenAi`, `AzureAiInference`, `Cohere`, `Conservative`). Chunk positions use UTF-16 offsets. Options are validated and snapshotted at construction/resolution. A failed batch is reported through `EmbeddingBatchResult` and the run continues; `IProgress<BulkEmbeddingProgress>` reports how far along ingestion is. Read-ahead is bounded by `MaxPendingBatches`, so memory does not grow with corpus size. Parsing, vector storage, retrieval, generation, and a real tokenizer (the `TokenEstimator` seam is character-based for now) are outside this package.

See the [RAG usage guide](https://github.com/alkampfergit/cisharpai/blob/main/wiki/rag.md) for host configuration, keyed providers, option tables, streaming, and failure handling.
