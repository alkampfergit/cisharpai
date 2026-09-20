using Cisharpai.Rag.Models;

namespace Cisharpai.Rag.Chunking;

public interface ITextChunker
{
    /// <summary>Splits a document lazily as an async stream, emitting chunks with source positions and optional metadata.</summary>
    IAsyncEnumerable<TextChunk> ChunkAsync(RagDocument document, CancellationToken cancellationToken = default);
}
