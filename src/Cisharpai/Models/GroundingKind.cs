namespace Cisharpai.Models;

/// <summary>
/// Indicates whether citations were produced by the provider's native grounding
/// mechanism or synthesized via prompt injection.
/// </summary>
public enum GroundingKind
{
    /// <summary>
    /// Citations were produced by the provider's native citation mechanism.
    /// </summary>
    Native,

    /// <summary>
    /// Citations were synthesized via prompt injection and marker parsing.
    /// Reliability characteristics differ from native citations.
    /// </summary>
    Synthesized,

    /// <summary>
    /// Citations were produced by a provider-hosted web search tool.
    /// The model invoked the search server-side; results are cited in the response.
    /// </summary>
    WebSearch
}
