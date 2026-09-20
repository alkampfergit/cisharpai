using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureAiInference.Models;

/// <summary>
/// Azure AI Inference response_format (follows Chat Completions API pattern).
/// </summary>
public sealed class AzureAiInferenceResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("json_schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AzureAiInferenceJsonSchemaSpec? JsonSchema { get; set; }
}

/// <summary>
/// The json_schema sub-object for Structured Outputs.
/// </summary>
public sealed class AzureAiInferenceJsonSchemaSpec
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
