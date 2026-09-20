using Cisharpai.Models;

namespace Cisharpai.Rag.Models;

/// <summary>An ordered batch outcome retaining provider metadata and optional raw payloads.</summary>
public sealed record EmbeddingBatchResult(
    long BatchIndex,
    IReadOnlyList<TextChunk> Chunks,
    IReadOnlyList<ChunkEmbedding> Items,
    EmbeddingResponse Response)
{
    public bool IsSuccess => Response.IsSuccess;
    public string? ErrorMessage => Response.ErrorMessage;
}
