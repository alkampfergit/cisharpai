namespace Cisharpai.Models;

/// <summary>
/// Options for prompt caching control. Specifies where cache breakpoints
/// should be placed in the request (Anthropic-specific).
/// </summary>
public sealed record PromptCachingOptions
{
    /// <summary>
    /// Zero-based indices into the message list where cache breakpoints should be placed.
    /// A breakpoint after message index N means "cache everything up to and including message N".
    /// </summary>
    public IReadOnlyList<int> MessageBreakpoints { get; init; } = [];

    /// <summary>
    /// When true, places a cache breakpoint on the system message/content.
    /// </summary>
    public bool CacheSystemMessage { get; init; }

    /// <summary>
    /// Zero-based indices into the tool definitions list where cache breakpoints should be placed.
    /// </summary>
    public IReadOnlyList<int> ToolBreakpoints { get; init; } = [];
}
