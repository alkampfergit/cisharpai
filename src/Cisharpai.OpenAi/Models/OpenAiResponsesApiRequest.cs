using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiResponsesApiRequest
{
    public string Model { get; set; } = string.Empty;

    public List<OpenAiChatMessage> Input { get; set; } = [];

    [JsonPropertyName("max_output_tokens")]
    public int? MaxOutputTokens { get; set; }

    public OpenAiReasoningOption? Reasoning { get; set; }

    public OpenAiTextOption? Text { get; set; }
}

public sealed class OpenAiReasoningOption
{
    public string Effort { get; set; } = string.Empty;
}

public sealed class OpenAiTextOption
{
    public string Verbosity { get; set; } = string.Empty;
}
