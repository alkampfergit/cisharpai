namespace Cisharpai.Models;

/// <summary>
/// Hints to the provider about the intended use of the embeddings.
/// </summary>
public enum EmbeddingInputType
{
    Query,
    Document,
    Classification,
    Clustering
}
