using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureOpenAi.Models;

public sealed class AzureOpenAiUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("prompt_tokens_details")]
    public AzureOpenAiPromptTokensDetails? PromptTokensDetails { get; set; }
}

public sealed class AzureOpenAiPromptTokensDetails
{
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; set; }
}

public sealed class AzureOpenAiChatChoice
{
    public AzureOpenAiChatMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class AzureOpenAiChatResponse
{
    public string Model { get; set; } = string.Empty;

    public List<AzureOpenAiChatChoice> Choices { get; set; } = [];

    public AzureOpenAiUsage Usage { get; set; } = new();
}
