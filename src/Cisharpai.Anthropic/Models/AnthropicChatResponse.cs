using System.Text.Json.Serialization;

namespace Cisharpai.Anthropic.Models;

public sealed class AnthropicContentBlock
{
    public string Type { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

public sealed class AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

public sealed class AnthropicChatResponse
{
    public string Model { get; set; } = string.Empty;

    public List<AnthropicContentBlock> Content { get; set; } = [];

    public AnthropicUsage Usage { get; set; } = new();

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}
