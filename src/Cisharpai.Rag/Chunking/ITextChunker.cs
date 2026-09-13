using Cisharpai.Rag.Models;

namespace Cisharpai.Rag.Chunking;

public interface ITextChunker
{
    /// <summary>Splits a document lazily as an async stream, preserving its text and source positions.</summary>
    IAsyncEnumerable<TextChunk> ChunkAsync(RagDocument document, CancellationToken cancellationToken = default);
}
