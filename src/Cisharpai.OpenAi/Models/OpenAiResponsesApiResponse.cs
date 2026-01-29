using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiResponsesApiResponse
{
    public string Id { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public List<OpenAiResponseOutput> Output { get; set; } = [];

    public OpenAiResponsesUsage Usage { get; set; } = new();
}

public sealed class OpenAiResponseOutput
{
    public string Type { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public List<OpenAiResponseContent> Content { get; set; } = [];
}

public sealed class OpenAiResponseContent
{
    public string Type { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

public sealed class OpenAiResponsesUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}
