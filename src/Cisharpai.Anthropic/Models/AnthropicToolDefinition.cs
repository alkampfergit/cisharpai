using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.Anthropic.Models;

public sealed class AnthropicToolDefinition
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [JsonPropertyName("input_schema")]
    public JsonElement InputSchema { get; set; }

    [JsonPropertyName("cache_control")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnthropicCacheControl? CacheControl { get; set; }
}

public sealed class AnthropicToolChoice
{
    public string Type { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }
}
