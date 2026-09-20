namespace Cisharpai.Models;

/// <summary>
/// Configures grounded chat (RAG) behavior for chat completions.
/// </summary>
public sealed record GroundedChatOptions(
    /// <summary>
    /// The documents to ground the model's response on.
    /// </summary>
    IReadOnlyList<DocumentChunk> Documents,

    /// <summary>
    /// The citation generation mode. Defaults to <see cref="CitationMode.Fast"/>, which is
    /// supported by every Cohere model that exposes citations (both the <c>command-r</c> and
    /// <c>command-a</c> families). <see cref="CitationMode.Accurate"/> is rejected by
    /// <c>command-a</c> models, so it is not safe as a cross-model default.
    /// </summary>
    CitationMode CitationMode = CitationMode.Fast)
{
    /// <summary>
    /// Validates the options. Throws <see cref="ArgumentException"/> if Documents is null or empty,
    /// or if any individual <see cref="DocumentChunk"/> is invalid.
    /// </summary>
    public void Validate()
    {
        if (Documents is null || Documents.Count == 0)
            throw new ArgumentException(
                "At least one document is required for grounded chat.", nameof(Documents));

        foreach (var document in Documents)
            document.Validate();
    }
}
