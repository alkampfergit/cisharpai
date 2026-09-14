namespace Cisharpai.Models;

/// <summary>
/// Specifies the citation generation mode for grounded chat.
/// Values express either citation <em>quality</em> (<see cref="Accurate"/>, <see cref="Fast"/>)
/// or <em>wire encoding</em> (<see cref="SearchResult"/>). See each value's remarks for details.
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
    Enabled,

    /// <summary>
    /// Anthropic-specific: emit <c>search_result</c> content blocks instead of
    /// <c>document</c> blocks, producing <c>search_result_location</c> citations
    /// that carry the caller-supplied <see cref="DocumentChunk.Source"/> and
    /// <see cref="DocumentChunk.Title"/> verbatim.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This value expresses <em>wire encoding</em> — which block type the provider
    /// receives — whereas <see cref="Accurate"/> and <see cref="Fast"/> express
    /// citation <em>quality</em> (a Cohere concept). The two axes are collapsed
    /// into a single-valued enum because the quality values are Cohere-specific:
    /// Anthropic already treats both as plain <see cref="Enabled"/>, so no
    /// meaningful combination is lost. A caller cannot request search-result
    /// blocks <em>and</em> a quality level at the same time; that is acceptable
    /// because there is no provider that honours both simultaneously.
    /// </para>
    /// <para>
    /// Other providers ignore this value (or warn-and-fallback) the same way
    /// Anthropic already ignores <see cref="Accurate"/> / <see cref="Fast"/>.
    /// </para>
    /// </remarks>
    SearchResult
}
