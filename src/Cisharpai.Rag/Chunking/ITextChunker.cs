using Cisharpai.Rag.Models;

namespace Cisharpai.Rag.Chunking;

public interface ITextChunker
{
    /// <summary>Splits a document lazily, preserving its text and source positions.</summary>
    IEnumerable<TextChunk> Chunk(RagDocument document);
}
