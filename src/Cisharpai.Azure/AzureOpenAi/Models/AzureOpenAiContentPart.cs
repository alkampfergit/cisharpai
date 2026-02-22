using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureOpenAi.Models;

/// <summary>
/// A content part in a multimodal Azure OpenAI chat message.
/// </summary>
public sealed class AzureOpenAiContentPart
{
    public string Type { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AzureOpenAiImageUrl? ImageUrl { get; set; }
}

/// <summary>
/// An image URL (or data URI) for an Azure OpenAI vision content part.
/// </summary>
public sealed class AzureOpenAiImageUrl
{
    public string Url { get; set; } = string.Empty;
}
