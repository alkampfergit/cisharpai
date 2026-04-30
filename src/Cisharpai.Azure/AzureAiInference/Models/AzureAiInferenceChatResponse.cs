using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureAiInference.Models;

public sealed class AzureAiInferenceUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public sealed class AzureAiInferenceChatChoice
{
    public int Index { get; set; }

    public AzureAiInferenceChatMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class AzureAiInferenceChatResponse
{
    public string Id { get; set; } = string.Empty;

    public string Object { get; set; } = string.Empty;

    public long Created { get; set; }

    public string Model { get; set; } = string.Empty;

    public List<AzureAiInferenceChatChoice> Choices { get; set; } = [];

    public AzureAiInferenceUsage? Usage { get; set; }
}
