using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("prompt_tokens_details")]
    public OpenAiPromptTokensDetails? PromptTokensDetails { get; set; }
}

public sealed class OpenAiPromptTokensDetails
{
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; set; }
}

public sealed class OpenAiChatChoice
{
    public OpenAiChatMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class OpenAiChatResponse
{
    public string Model { get; set; } = string.Empty;

    public List<OpenAiChatChoice> Choices { get; set; } = [];

    public OpenAiUsage Usage { get; set; } = new();
}
