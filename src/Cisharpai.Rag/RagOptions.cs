using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Embeddings;

namespace Cisharpai.Rag;

/// <summary>RAG ingestion settings; provider credentials and endpoints are configured separately.</summary>
public sealed class RagOptions
{
    public FixedSizeChunkerOptions Chunking { get; set; } = new();
    public BulkEmbeddingOptions Embedding { get; set; } = new();
}
