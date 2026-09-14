namespace Cisharpai.Anthropic.Models;

/// <summary>
/// Common interface for Anthropic content blocks that carry document data
/// (<see cref="AnthropicDocumentBlock"/> and <see cref="AnthropicSearchResultBlock"/>),
/// enabling type-safe handling without downcasting to <c>object</c>.
/// </summary>
public interface IAnthropicDocumentContentBlock
{
    AnthropicCacheControl? CacheControl { get; set; }
}
