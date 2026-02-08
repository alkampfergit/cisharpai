using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureOpenAi.Models;

/// <summary>
/// Azure OpenAI response_format (same structure as OpenAI Chat Completions API).
/// </summary>
public sealed class AzureOpenAiResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("json_schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AzureOpenAiJsonSchemaSpec? JsonSchema { get; set; }
}

/// <summary>
/// The json_schema sub-object for Structured Outputs.
/// </summary>
public sealed class AzureOpenAiJsonSchemaSpec
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
