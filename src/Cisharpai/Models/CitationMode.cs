namespace Cisharpai.Models;

/// <summary>
/// Specifies the citation generation mode for grounded chat.
/// </summary>
public enum CitationMode
{
    /// <summary>
    /// Generates accurate, fine-grained citations. Higher latency.
    /// </summary>
    /// <remarks>
    /// Not supported by Cohere's <c>command-a</c> model family (e.g. <c>command-a-03-2025</c>);
    /// only the <c>command-r</c> family accepts <c>ACCURATE</c>. When this mode is requested
    /// against a <c>command-a</c> model the Cohere provider logs a warning and falls back to
    /// <see cref="Fast"/> instead of forwarding the unsupported value to the API.
    /// </remarks>
    Accurate,

    /// <summary>
    /// Generates citations quickly with less granularity.
    /// </summary>
    Fast,

    /// <summary>
    /// Enables citations with provider-default behavior.
    /// </summary>
    Enabled
}
