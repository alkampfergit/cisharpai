namespace Cisharpai.Rag.Chunking;

public enum SemanticThresholdStrategy
{
    /// <summary>
    /// Boundary at the lowest percentile of similarity drops in this document.
    /// Self-calibrating across models and domains. Buffers all sentence embeddings
    /// in memory before emitting the first chunk.
    /// </summary>
    Percentile,

    /// <summary>
    /// Boundary when cosine similarity drops below an absolute threshold.
    /// The right number varies by embedding model and domain — tune per model.
    /// Enables true streaming: chunks can be emitted as embedding batches return.
    /// </summary>
    Absolute
}
