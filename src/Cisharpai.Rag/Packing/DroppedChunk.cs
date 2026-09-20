namespace Cisharpai.Rag.Packing;

/// <summary>
/// A chunk that was not included in the packed context, with its token count and the reason it was dropped.
/// </summary>
/// <param name="Chunk">The scored chunk that was dropped.</param>
/// <param name="TokenCount">The token count of the chunk text.</param>
/// <param name="Reason">Why the chunk was dropped.</param>
public sealed record DroppedChunk(ScoredChunk Chunk, int TokenCount, DropReason Reason);
