using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
}

public sealed class OpenAiChatChoice
{
    public OpenAiChatMessage Message { get; set; } = new();
}

public sealed class OpenAiChatResponse
{
    public string Model { get; set; } = string.Empty;

    public List<OpenAiChatChoice> Choices { get; set; } = [];

    public OpenAiUsage Usage { get; set; } = new();
}
