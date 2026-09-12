namespace Cisharpai.Rag.Models;

public sealed record BulkEmbeddingProgress(
    long CompletedBatches,
    long TotalChunksProcessed,
    long FailedBatches);
