namespace Cisharpai.Models;

/// <summary>
/// Represents a source document that backs a citation.
/// </summary>
public sealed record CitationSource(
    /// <summary>
    /// The document identifier.
    /// </summary>
    string Id,

    /// <summary>
    /// The source document data as key-value pairs (if the source was structured).
    /// </summary>
    IReadOnlyDictionary<string, string>? Data = null);
