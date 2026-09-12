using Cisharpai.Rag.Models;

namespace Cisharpai.Rag.Embeddings;

public interface IBulkEmbeddingProcessor
{
    IAsyncEnumerable<EmbeddingBatchResult> EmbedAsync(
        IAsyncEnumerable<TextChunk> chunks,
        IProgress<BulkEmbeddingProgress>? progress = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<EmbeddingBatchResult> EmbedAsync(
        IEnumerable<TextChunk> chunks,
        IProgress<BulkEmbeddingProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
