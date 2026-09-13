using Cisharpai.Rag.Models;

namespace Cisharpai.Rag.Packing;

/// <summary>
/// A ranked chunk with its relevance score, as produced by a retrieval or reranking stage.
/// </summary>
/// <param name="Chunk">The text chunk.</param>
/// <param name="Score">Relevance score. <c>double</c> to accept both <c>float</c> similarity scores
/// and <c>double</c> reranker scores without precision loss.</param>
public sealed record ScoredChunk(TextChunk Chunk, double Score);
