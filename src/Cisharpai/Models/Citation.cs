namespace Cisharpai.Models;

/// <summary>
/// Represents a citation in a grounded chat response.
/// References a span of text in the response content backed by source documents.
/// </summary>
public sealed record Citation(
    /// <summary>
    /// Start character offset (inclusive) in the response content.
    /// </summary>
    int Start,

    /// <summary>
    /// End character offset (exclusive) in the response content.
    /// </summary>
    int End,

    /// <summary>
    /// The cited text span from the response content.
    /// </summary>
    string Text,

    /// <summary>
    /// The source documents backing this citation.
    /// </summary>
    IReadOnlyList<CitationSource> Sources,

    /// <summary>
    /// The citation type (e.g., "TEXT_CONTENT" for Cohere). Null if not provided by the provider.
    /// </summary>
    string? Type = null);
