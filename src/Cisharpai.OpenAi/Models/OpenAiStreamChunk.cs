using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiStreamDelta
{
    public string? Content { get; set; }
    public string? Role { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAiStreamToolCallDelta>? ToolCalls { get; set; }
}

public sealed class OpenAiStreamToolCallDelta
{
    public int Index { get; set; }
    public string? Id { get; set; }
    public OpenAiStreamToolCallFunction? Function { get; set; }
}

public sealed class OpenAiStreamToolCallFunction
{
    public string? Name { get; set; }
    public string? Arguments { get; set; }
}

public sealed class OpenAiStreamChoice
{
    public OpenAiStreamDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    public int Index { get; set; }
}

public sealed class OpenAiStreamChunk
{
    public string? Id { get; set; }
    public string? Model { get; set; }
    public List<OpenAiStreamChoice>? Choices { get; set; }
    public OpenAiUsage? Usage { get; set; }
}
