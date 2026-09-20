using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureOpenAi.Models;

public sealed class AzureOpenAiEmbeddingRequest
{
    public object Input { get; set; } = string.Empty;

    [JsonPropertyName("encoding_format")]
    public string? EncodingFormat { get; set; }

    public int? Dimensions { get; set; }

    public string? User { get; set; }
}
