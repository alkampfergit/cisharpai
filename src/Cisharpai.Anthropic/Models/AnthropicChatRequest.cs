using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.Anthropic.Models;

public sealed class AnthropicMessage
{
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Content can be a string (normal messages) or a list of content blocks
    /// (for tool_use/tool_result). Serialized as-is by System.Text.Json.
    /// </summary>
    public object Content { get; set; } = string.Empty;
}

public sealed class AnthropicChatRequest
{
    public string Model { get; set; } = string.Empty;

    public List<AnthropicMessage> Messages { get; set; } = [];

    public string? System { get; set; }

    public double? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("output_config")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnthropicOutputConfig? OutputConfig { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AnthropicToolDefinition>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnthropicToolChoice? ToolChoice { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Stream { get; set; }
}
