namespace Cisharpai.Rag.Pipeline;

/// <summary>
/// A single chunk from a streaming RAG pipeline response.
/// Intermediate chunks carry <see cref="ContentDelta"/>; the final chunk carries <see cref="FinalResult"/>.
/// </summary>
public sealed record RagStreamingChunk
{
    public string? ContentDelta { get; init; }
    public string? FinishReason { get; init; }
    public RagResult? FinalResult { get; init; }
}
