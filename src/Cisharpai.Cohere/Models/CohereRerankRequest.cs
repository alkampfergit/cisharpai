using System.Text.Json.Serialization;

namespace Cisharpai.Cohere.Models;

public sealed class CohereRerankRequest
{
    public string Model { get; set; } = string.Empty;

    public string Query { get; set; } = string.Empty;

    public List<string> Documents { get; set; } = [];

    [JsonPropertyName("top_n")]
    public int? TopN { get; set; }

    [JsonPropertyName("max_tokens_per_doc")]
    public int? MaxTokensPerDoc { get; set; }
}
