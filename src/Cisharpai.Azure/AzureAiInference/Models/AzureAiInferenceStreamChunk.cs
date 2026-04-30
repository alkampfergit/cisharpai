using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureAiInference.Models;

public sealed class AzureAiInferenceStreamDelta
{
    public string? Content { get; set; }
    public string? Role { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<AzureAiInferenceStreamToolCallDelta>? ToolCalls { get; set; }
}

public sealed class AzureAiInferenceStreamToolCallDelta
{
    public int Index { get; set; }
    public string? Id { get; set; }
    public AzureAiInferenceStreamToolCallFunction? Function { get; set; }
}

public sealed class AzureAiInferenceStreamToolCallFunction
{
    public string? Name { get; set; }
    public string? Arguments { get; set; }
}

public sealed class AzureAiInferenceStreamChoice
{
    public AzureAiInferenceStreamDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    public int Index { get; set; }
}

public sealed class AzureAiInferenceStreamChunk
{
    public string? Id { get; set; }
    public string? Model { get; set; }
    public List<AzureAiInferenceStreamChoice>? Choices { get; set; }
    public AzureAiInferenceUsage? Usage { get; set; }
}
