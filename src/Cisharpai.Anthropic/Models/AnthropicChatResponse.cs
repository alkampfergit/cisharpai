using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.Anthropic.Models;

public sealed class AnthropicContentBlock
{
    public string Type { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    /// <summary>For tool_use content blocks: the tool call id.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>For tool_use content blocks: the function name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>For tool_use content blocks: the arguments as JSON.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Input { get; set; }

    /// <summary>For tool_result content blocks: the tool call id being responded to.</summary>
    [JsonPropertyName("tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolUseId { get; set; }

    /// <summary>For tool_result content blocks: the result text content.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    /// <summary>For tool_result content blocks: whether the tool execution failed.</summary>
    [JsonPropertyName("is_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsError { get; set; }

    /// <summary>For image content blocks: the image source (base64 data and media type).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnthropicImageSource? Source { get; set; }

    /// <summary>For response text blocks: citation results from grounded chat.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AnthropicCitationResult>? Citations { get; set; }

    [JsonPropertyName("cache_control")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnthropicCacheControl? CacheControl { get; set; }
}

public sealed class AnthropicCacheControl
{
    public string Type { get; set; } = "ephemeral";
}

public sealed class AnthropicSystemBlock
{
    public string Type { get; set; } = "text";

    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("cache_control")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnthropicCacheControl? CacheControl { get; set; }
}

public sealed class AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("cache_creation_input_tokens")]
    public int? CacheCreationInputTokens { get; set; }

    [JsonPropertyName("cache_read_input_tokens")]
    public int? CacheReadInputTokens { get; set; }
}

public sealed class AnthropicChatResponse
{
    public string Model { get; set; } = string.Empty;

    public List<AnthropicContentBlock> Content { get; set; } = [];

    public AnthropicUsage Usage { get; set; } = new();

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}
