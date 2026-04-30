using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

/// <summary>
/// A content part in a multimodal OpenAI chat message.
/// </summary>
public sealed class OpenAiContentPart
{
    public string Type { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiImageUrl? ImageUrl { get; set; }
}

/// <summary>
/// An image URL (or data URI) for an OpenAI vision content part.
/// </summary>
public sealed class OpenAiImageUrl
{
    public string Url { get; set; } = string.Empty;
}
