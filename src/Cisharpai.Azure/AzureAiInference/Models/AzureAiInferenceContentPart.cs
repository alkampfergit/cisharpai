using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureAiInference.Models;

/// <summary>
/// A content part in a multimodal Azure AI Inference chat message.
/// </summary>
public sealed class AzureAiInferenceContentPart
{
    public string Type { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AzureAiInferenceImageUrl? ImageUrl { get; set; }
}

/// <summary>
/// An image URL (or data URI) for an Azure AI Inference vision content part.
/// </summary>
public sealed class AzureAiInferenceImageUrl
{
    public string Url { get; set; } = string.Empty;
}
