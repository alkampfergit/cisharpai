using Cisharpai.Rag.Models;

namespace Cisharpai.Rag;

/// <summary>Composes document chunking and bulk embeddings, yielding each completed batch.</summary>
public interface IRagIngestionPipeline
{
    IAsyncEnumerable<EmbeddingBatchResult> IngestAsync(
        IAsyncEnumerable<RagDocument> documents, CancellationToken cancellationToken = default);

    IAsyncEnumerable<EmbeddingBatchResult> IngestAsync(
        IEnumerable<RagDocument> documents, CancellationToken cancellationToken = default);
}
