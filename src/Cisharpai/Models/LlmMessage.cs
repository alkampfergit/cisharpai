namespace Cisharpai.Models;

/// <summary>
/// Represents a message in a conversation.
/// </summary>
public sealed record LlmMessage(
    LlmRole Role,
    string Content,

    /// <summary>
    /// For role=Tool messages: identifies which tool call this message responds to.
    /// </summary>
    string? ToolCallId = null,

    /// <summary>
    /// For role=Assistant messages: tool invocations requested by the model.
    /// </summary>
    IReadOnlyList<ToolCall>? ToolCalls = null);
