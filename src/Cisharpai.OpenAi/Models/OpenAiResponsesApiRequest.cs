using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiResponsesApiRequest
{
    public string Model { get; set; } = string.Empty;

    public List<object> Input { get; set; } = [];

    [JsonPropertyName("max_output_tokens")]
    public int? MaxOutputTokens { get; set; }

    public OpenAiReasoningOption? Reasoning { get; set; }

    public OpenAiTextOption? Text { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpenAiResponsesApiTool>? Tools { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Include { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Stream { get; set; }
}

public sealed class OpenAiResponsesApiTool
{
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("vector_store_ids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? VectorStoreIds { get; set; }

    [JsonPropertyName("max_num_results")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxNumResults { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiFileSearchFilter? Filters { get; set; }

    [JsonPropertyName("ranking_options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiFileSearchRankingOptions? RankingOptions { get; set; }
}

public class OpenAiFileSearchFilter
{
    public string Type { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Key { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Value { get; set; }

    [JsonPropertyName("filters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpenAiFileSearchFilter>? SubFilters { get; set; }
}

public sealed class OpenAiFileSearchRankingOptions
{
    [JsonPropertyName("score_threshold")]
    public double ScoreThreshold { get; set; }
}

public sealed class OpenAiReasoningOption
{
    public string Effort { get; set; } = string.Empty;
}

public sealed class OpenAiTextOption
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Verbosity { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiTextFormat? Format { get; set; }
}
