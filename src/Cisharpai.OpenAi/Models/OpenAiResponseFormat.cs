using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

/// <summary>
/// OpenAI response_format for Chat Completions API.
/// </summary>
public sealed class OpenAiResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("json_schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiJsonSchemaSpec? JsonSchema { get; set; }
}

/// <summary>
/// The json_schema sub-object for Structured Outputs.
/// </summary>
public sealed class OpenAiJsonSchemaSpec
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("strict")]
    public bool Strict { get; set; } = true;

    [JsonPropertyName("schema")]
    public JsonElement Schema { get; set; }
}

/// <summary>
/// OpenAI text.format for Responses API (GPT-5).
/// For json_schema, the fields are flattened into the format object.
/// </summary>
public sealed class OpenAiTextFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("strict")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Strict { get; set; }

    [JsonPropertyName("schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Schema { get; set; }
}
