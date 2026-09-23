namespace Cisharpai.Rag;

/// <summary>
/// Marker interface for provider-specific retrieval query extensions.
/// </summary>
public interface IRetrievalQueryExtension;

/// <summary>
/// Common retrieval options shared by all retriever implementations.
/// </summary>
public sealed record RetrievalOptions
{
    /// <summary>
    /// Maximum number of chunks to return. When null, the retriever default is used.
    /// </summary>
    public int? TopK { get; init; }

    /// <summary>
    /// Minimum score threshold applied to <see cref="ScoredChunk.Score"/>.
    /// Score scales are provider-specific and not directly comparable across backends.
    /// </summary>
    public double? MinScore { get; init; }

    /// <summary>
    /// Portable metadata equality filters combined with logical AND.
    /// </summary>
    public IReadOnlyDictionary<string, string>? MetadataEquals { get; init; }

    /// <summary>
    /// Optional provider-specific query extension.
    /// </summary>
    public IRetrievalQueryExtension? ProviderQuery { get; init; }
}
