namespace Cisharpai.Models;

/// <summary>
/// Specifies the citation generation mode for grounded chat.
/// </summary>
public enum CitationMode
{
    /// <summary>
    /// Generates accurate, fine-grained citations. Higher latency.
    /// </summary>
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
