using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.Anthropic.Models;

/// <summary>
/// Anthropic output_config parameter for structured outputs.
/// </summary>
public sealed class AnthropicOutputConfig
{
    [JsonPropertyName("format")]
    public AnthropicOutputFormat Format { get; set; } = new();
}

/// <summary>
/// Anthropic output format specifying json_schema type and schema.
/// </summary>
public sealed class AnthropicOutputFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Schema { get; set; }
}
