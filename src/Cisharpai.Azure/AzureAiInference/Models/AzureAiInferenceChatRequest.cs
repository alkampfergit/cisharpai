using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureAiInference.Models;

public sealed class AzureAiInferenceChatMessage
{
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public sealed class AzureAiInferenceChatRequest
{
    public string? Model { get; set; }

    public List<AzureAiInferenceChatMessage> Messages { get; set; } = [];

    public double? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }

    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; set; }

    public List<string>? Stop { get; set; }
}

public sealed class AzureAiInferenceReasoningChatRequest
{
    public string? Model { get; set; }

    public List<AzureAiInferenceChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("max_completion_tokens")]
    public int? MaxCompletionTokens { get; set; }
}
