using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureAiInference.Models;

public sealed class AzureAiInferenceChatMessage
{
    public string Role { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Content { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Refusal { get; set; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AzureAiInferenceToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; set; }
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

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AzureAiInferenceResponseFormat? ResponseFormat { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AzureAiInferenceToolDefinition>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ToolChoice { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Stream { get; set; }

    [JsonPropertyName("stream_options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AzureAiInferenceStreamOptions? StreamOptions { get; set; }
}

public sealed class AzureAiInferenceReasoningChatRequest
{
    public string? Model { get; set; }

    public List<AzureAiInferenceChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("max_completion_tokens")]
    public int? MaxCompletionTokens { get; set; }

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AzureAiInferenceResponseFormat? ResponseFormat { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AzureAiInferenceToolDefinition>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ToolChoice { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Stream { get; set; }

    [JsonPropertyName("stream_options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AzureAiInferenceStreamOptions? StreamOptions { get; set; }
}

public sealed class AzureAiInferenceStreamOptions
{
    [JsonPropertyName("include_usage")]
    public bool IncludeUsage { get; set; } = true;
}
