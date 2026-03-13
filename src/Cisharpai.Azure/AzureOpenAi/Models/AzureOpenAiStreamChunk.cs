using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureOpenAi.Models;

public sealed class AzureOpenAiStreamDelta
{
    public string? Content { get; set; }
    public string? Role { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<AzureOpenAiStreamToolCallDelta>? ToolCalls { get; set; }
}

public sealed class AzureOpenAiStreamToolCallDelta
{
    public int Index { get; set; }
    public string? Id { get; set; }
    public AzureOpenAiStreamToolCallFunction? Function { get; set; }
}

public sealed class AzureOpenAiStreamToolCallFunction
{
    public string? Name { get; set; }
    public string? Arguments { get; set; }
}

public sealed class AzureOpenAiStreamChoice
{
    public AzureOpenAiStreamDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    public int Index { get; set; }
}

public sealed class AzureOpenAiStreamChunk
{
    public string? Id { get; set; }
    public string? Model { get; set; }
    public List<AzureOpenAiStreamChoice>? Choices { get; set; }
    public AzureOpenAiUsage? Usage { get; set; }
}
