using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureAiInference.Models;

public sealed class AzureAiInferenceEmbeddingRequest
{
    public string? Model { get; set; }

    /// <summary>
    /// The input text(s) to embed.
    /// </summary>
    public IReadOnlyList<string> Input { get; set; } = [];

    public int? Dimensions { get; set; }

    [JsonPropertyName("encoding_format")]
    public string? EncodingFormat { get; set; }

    [JsonPropertyName("input_type")]
    public string? InputType { get; set; }
}
