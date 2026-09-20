using System.Text.Json.Serialization;

namespace Cisharpai.Cohere.Models;

public sealed class CohereEmbedInput
{
    [JsonPropertyName("content")]
    public List<CohereEmbedContentPart> Content { get; set; } = [];
}

public sealed class CohereEmbedContentPart
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    public CohereImageUrl? ImageUrl { get; set; }
}

public sealed class CohereImageUrl
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
