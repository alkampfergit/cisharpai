namespace Cisharpai.Models;

/// <summary>
/// Represents the output of executing a tool, sent back to the model.
/// </summary>
public sealed record ToolResult(
    /// <summary>
    /// The identifier of the tool call this result responds to.
    /// Must match <see cref="ToolCall.Id"/>.
    /// </summary>
    string ToolCallId,

    /// <summary>
    /// The textual content returned by the tool.
    /// </summary>
    string Content,

    /// <summary>
    /// When true, indicates the tool execution failed.
    /// Defaults to false.
    /// </summary>
    bool IsError = false);
