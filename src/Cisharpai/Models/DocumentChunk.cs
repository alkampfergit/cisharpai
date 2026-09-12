namespace Cisharpai.Models;

/// <summary>
/// Represents a document chunk for grounded chat (RAG).
/// Exactly one of <see cref="Data"/> or <see cref="Text"/> must be set.
/// </summary>
public sealed record DocumentChunk(
    /// <summary>
    /// Optional document identifier. Used to trace citations back to source documents.
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
