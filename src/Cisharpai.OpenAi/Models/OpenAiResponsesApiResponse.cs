using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiResponsesApiResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("incomplete_details")]
    public OpenAiIncompleteDetails? IncompleteDetails { get; set; }

    [JsonPropertyName("output")]
    public List<OpenAiResponseOutput> Output { get; set; } = [];

    [JsonPropertyName("usage")]
    public OpenAiResponsesUsage Usage { get; set; } = new();
}

public sealed class OpenAiIncompleteDetails
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class OpenAiResponseOutput
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public List<OpenAiResponseContent> Content { get; set; } = [];
}

public sealed class OpenAiResponseContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("refusal")]
    public string? Refusal { get; set; }
}

public sealed class OpenAiResponsesUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("output_tokens_details")]
    public OpenAiOutputTokensDetails? OutputTokensDetails { get; set; }
}

public sealed class OpenAiOutputTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; set; }
}
