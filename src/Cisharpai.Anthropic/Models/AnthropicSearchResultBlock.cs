using System.Text.Json.Serialization;

namespace Cisharpai.Anthropic.Models;

/// <summary>
/// Request-side content block for Anthropic <c>search_result</c> citations.
/// Emitted when <see cref="Cisharpai.Models.CitationMode.SearchResult"/> is active.
/// The caller's <see cref="Source"/> and <see cref="Title"/> are passed through
/// verbatim in <c>search_result_location</c> citations on the response.
/// </summary>
public sealed class AnthropicSearchResultBlock : IAnthropicDocumentContentBlock
{
    public string Type { get; set; } = "search_result";

    public string Source { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    public List<AnthropicCustomContentBlock> Content { get; set; } = [];

    public AnthropicCitationConfig Citations { get; set; } = new() { Enabled = true };

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("cache_control")]
    public AnthropicCacheControl? CacheControl { get; set; }
}
