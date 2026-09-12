# Cisharpai.Rag

Provider-independent RAG ingestion foundations for .NET 8 and .NET 10: fixed-size text chunking, sequential bulk float embeddings, and a composable document pipeline using `IEmbeddingClient`.

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
        BatchSize = 32,
        Model = "text-embedding-3-small"
    }));

await foreach (var batch in pipeline.IngestAsync(new[]
{
    new RagDocument("guide", "Your document text")
}))
{
    if (!batch.IsSuccess)
    {
        Console.Error.WriteLine(batch.ErrorMessage);
        break;
    }
    foreach (var item in batch.Items)
        Console.WriteLine($"{item.Chunk.DocumentId}:{item.Chunk.Index}: {item.Vector.Length}");
}
```

Register a provider separately, then use `services.AddCisharpaiRag(options => ...)` for DI. A factory overload selects keyed embedding clients. The same pipeline accepts `IAsyncEnumerable<RagDocument>` and cancellation; `BulkEmbeddingProcessor` also accepts existing chunk streams.

Defaults: chunk size 1024 Unicode scalar values, overlap 128, batch size 32, document input type, float vectors. Chunk positions use UTF-16 offsets. Options are validated and snapshotted at construction/resolution. Batching buffers one batch plus the current document and stops after a failed batch; prior successes remain available. Token limits, parsing, vector storage, retrieval, generation, and ingestion-level retries are outside this package.

See the [RAG usage guide](https://github.com/alkampfergit/cisharpai/blob/main/wiki/rag.md) for host configuration, keyed providers, option tables, streaming, and failure handling.
