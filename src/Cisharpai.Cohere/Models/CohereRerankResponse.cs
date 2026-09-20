using System.Text.Json.Serialization;

namespace Cisharpai.Cohere.Models;

public sealed class CohereRerankResult
{
    public int Index { get; set; }

    [JsonPropertyName("relevance_score")]
    public double RelevanceScore { get; set; }
}

public sealed class CohereRerankBilledUnits
{
    [JsonPropertyName("search_units")]
    public int? SearchUnits { get; set; }

    [JsonPropertyName("input_tokens")]
    public int? InputTokens { get; set; }
}

public sealed class CohereRerankMeta
{
    [JsonPropertyName("billed_units")]
    public CohereRerankBilledUnits BilledUnits { get; set; } = new();
}

public sealed class CohereRerankResponse
{
    public string Id { get; set; } = string.Empty;

    public List<CohereRerankResult> Results { get; set; } = [];

    public CohereRerankMeta Meta { get; set; } = new();
}
