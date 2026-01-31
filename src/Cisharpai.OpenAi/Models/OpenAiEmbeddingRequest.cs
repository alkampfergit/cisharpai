using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiEmbeddingRequest
{
    public string Model { get; set; } = string.Empty;

    public object Input { get; set; } = string.Empty;

    [JsonPropertyName("encoding_format")]
    public string? EncodingFormat { get; set; }

    public int? Dimensions { get; set; }

    public string? User { get; set; }
}
