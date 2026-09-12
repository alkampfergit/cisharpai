using System.Text.Json.Serialization;

namespace Cisharpai.Anthropic.Models;

public sealed class AnthropicDocumentSource
{
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("media_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaType { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Data { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AnthropicCustomContentBlock>? Content { get; set; }
}

public sealed class AnthropicCustomContentBlock
{
    public string Type { get; set; } = "text";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }
}

public sealed class AnthropicCitationConfig
{
    public bool Enabled { get; set; }
}

public sealed class AnthropicCitationResult
{
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("cited_text")]
    public string? CitedText { get; set; }

    [JsonPropertyName("document_index")]
    public int? DocumentIndex { get; set; }

    [JsonPropertyName("document_title")]
    public string? DocumentTitle { get; set; }

    [JsonPropertyName("start_char_index")]
    public int? StartCharIndex { get; set; }

    [JsonPropertyName("end_char_index")]
    public int? EndCharIndex { get; set; }
}
