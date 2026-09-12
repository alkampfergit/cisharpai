using System.Runtime.CompilerServices;
using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Embeddings;
using Cisharpai.Rag.Models;

namespace Cisharpai.Rag;

public sealed class RagIngestionPipeline : IRagIngestionPipeline
{
    private readonly ITextChunker _chunker;
    private readonly IBulkEmbeddingProcessor _processor;

    public RagIngestionPipeline(ITextChunker chunker, IBulkEmbeddingProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(chunker);
        ArgumentNullException.ThrowIfNull(processor);
        _chunker = chunker;
        _processor = processor;
    }

    public IAsyncEnumerable<EmbeddingBatchResult> IngestAsync(
        IEnumerable<RagDocument> documents,
        IProgress<BulkEmbeddingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        return _processor.EmbedAsync(
            ChunkDocumentsAsync(AsAsync(documents, cancellationToken), cancellationToken),
            progress,
            cancellationToken);
    }

    public IAsyncEnumerable<EmbeddingBatchResult> IngestAsync(
        IAsyncEnumerable<RagDocument> documents,
        IProgress<BulkEmbeddingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        return _processor.EmbedAsync(
            ChunkDocumentsAsync(documents, cancellationToken),
            progress,
            cancellationToken);
    }

    private static async IAsyncEnumerable<RagDocument> AsAsync(
        IEnumerable<RagDocument> documents,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        using var enumerator = documents.GetEnumerator();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (!enumerator.MoveNext()) yield break;
            token.ThrowIfCancellationRequested();
            yield return enumerator.Current;
        }
    }

    private async IAsyncEnumerable<TextChunk> ChunkDocumentsAsync(
        IAsyncEnumerable<RagDocument> documents,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        await foreach (var document in documents.WithCancellation(token).ConfigureAwait(false))
        {
            token.ThrowIfCancellationRequested();
            foreach (var chunk in _chunker.Chunk(document))
            {
                token.ThrowIfCancellationRequested();
                yield return chunk;
            }
        }
    }
}
