using System.Text.Json.Serialization;

namespace Cisharpai.Cohere.Models;

public sealed class CohereEmbedRequest
{
    public string Model { get; set; } = string.Empty;

    public List<string> Texts { get; set; } = [];

    [JsonPropertyName("input_type")]
    public string InputType { get; set; } = "search_document";

    [JsonPropertyName("embedding_types")]
    public List<string>? EmbeddingTypes { get; set; }

    public string? Truncate { get; set; }
}
