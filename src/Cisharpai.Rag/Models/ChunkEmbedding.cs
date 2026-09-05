namespace Cisharpai.Rag.Models;

/// <summary>A source chunk paired with its provider's float embedding vector.</summary>
public sealed record ChunkEmbedding(TextChunk Chunk, float[] Vector);
