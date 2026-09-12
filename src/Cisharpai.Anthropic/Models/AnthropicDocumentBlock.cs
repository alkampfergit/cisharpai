using System.Text.Json.Serialization;

namespace Cisharpai.Anthropic.Models;

/// <summary>
/// Request-side content block for Anthropic document citations.
/// Separate from <see cref="AnthropicContentBlock"/> to avoid JSON property
/// conflicts on the 'source' field (image vs document have different shapes).
/// </summary>
public sealed class AnthropicDocumentBlock
{
    public string Type { get; set; } = "document";

    public AnthropicDocumentSource Source { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    public AnthropicCitationConfig Citations { get; set; } = new() { Enabled = true };
}
