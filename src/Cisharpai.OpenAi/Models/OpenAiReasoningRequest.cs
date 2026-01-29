using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiReasoningRequest
{
    public string Model { get; set; } = string.Empty;

    public List<OpenAiChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("max_completion_tokens")]
    public int? MaxCompletionTokens { get; set; }
}
