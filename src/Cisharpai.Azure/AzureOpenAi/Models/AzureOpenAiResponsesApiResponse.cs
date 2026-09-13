using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureOpenAi.Models;

public sealed class AzureOpenAiResponsesApiResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("incomplete_details")]
    public AzureOpenAiIncompleteDetails? IncompleteDetails { get; set; }

    [JsonPropertyName("output")]
    public List<AzureOpenAiResponseOutput> Output { get; set; } = [];

    [JsonPropertyName("usage")]
    public AzureOpenAiResponsesUsage Usage { get; set; } = new();
}

public sealed class AzureOpenAiIncompleteDetails
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AzureOpenAiResponseOutput
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public List<AzureOpenAiResponseContent> Content { get; set; } = [];
}

public sealed class AzureOpenAiResponseContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("refusal")]
    public string? Refusal { get; set; }

    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AzureOpenAiAnnotation>? Annotations { get; set; }
}

public sealed class AzureOpenAiAnnotation
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("file_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileId { get; set; }

    [JsonPropertyName("filename")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Filename { get; set; }

    [JsonPropertyName("index")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Index { get; set; }

    [JsonPropertyName("start_index")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StartIndex { get; set; }

    [JsonPropertyName("end_index")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EndIndex { get; set; }
}

public sealed class AzureOpenAiResponsesUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("output_tokens_details")]
    public AzureOpenAiOutputTokensDetails? OutputTokensDetails { get; set; }

    [JsonPropertyName("input_tokens_details")]
    public AzureOpenAiInputTokensDetails? InputTokensDetails { get; set; }
}

public sealed class AzureOpenAiOutputTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; set; }
}

public sealed class AzureOpenAiInputTokensDetails
{
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; set; }
}
