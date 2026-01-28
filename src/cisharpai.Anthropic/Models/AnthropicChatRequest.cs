using System.Text.Json.Serialization;

namespace cisharpai.Anthropic.Models;

public sealed class AnthropicMessage
{
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public sealed class AnthropicChatRequest
{
    public string Model { get; set; } = string.Empty;

    public List<AnthropicMessage> Messages { get; set; } = [];

    public string? System { get; set; }

    public double? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1024;
}
