using System.Text.Json.Serialization;

namespace cisharpai.AzureOpenAi.Models;

public sealed class AzureOpenAiUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
}

public sealed class AzureOpenAiChatChoice
{
    public AzureOpenAiChatMessage Message { get; set; } = new();
}

public sealed class AzureOpenAiChatResponse
{
    public string Model { get; set; } = string.Empty;

    public List<AzureOpenAiChatChoice> Choices { get; set; } = [];

    public AzureOpenAiUsage Usage { get; set; } = new();
}
