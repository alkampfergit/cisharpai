namespace Cisharpai.Models;

/// <summary>
/// Represents a document chunk for grounded chat (RAG).
/// Exactly one of <see cref="Data"/> or <see cref="Text"/> must be set.
/// </summary>
public sealed record DocumentChunk(
    /// <summary>
    /// Optional internal document identifier. Used to trace citations back to
    /// source documents in your own system (a primary key, a database row id, etc.).
    /// Distinct from <see cref="Source"/>, which is the caller-facing identifier
    /// passed through verbatim in citations.
    /// </summary>
    string? Id = null,

    /// <summary>
    /// Structured document data as key-value pairs (e.g., title, snippet).
    /// Mutually exclusive with <see cref="Text"/>.
    /// </summary>
    IReadOnlyDictionary<string, string>? Data = null,

    /// <summary>
    /// Plain text document content.
    /// Mutually exclusive with <see cref="Data"/>.
    /// </summary>
    string? Text = null)
{
    /// <summary>
    /// Caller-facing source identifier (a URL, a document path, a permanent link)
    /// passed through verbatim in citations. Required when using
    /// <see cref="CitationMode.SearchResult"/>; optional otherwise.
    /// Other providers ignore this property for now.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Human-readable display title for this document chunk.
    /// When using <see cref="CitationMode.SearchResult"/>, passed through
    /// verbatim in citations. Optional; other providers ignore this for now.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Validates that exactly one of Data or Text is provided.
    /// </summary>
    public void Validate()
    {
        var hasData = Data is not null && Data.Count > 0;
        var hasText = !string.IsNullOrWhiteSpace(Text);

        if (!hasData && !hasText)
            throw new ArgumentException(
                "Either Data or Text must be provided for a document chunk.");

        if (hasData && hasText)
            throw new ArgumentException(
                "Data and Text are mutually exclusive for a document chunk.");
    }
}
