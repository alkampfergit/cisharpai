using System.Text.Json;

namespace Cisharpai.Models;

/// <summary>
/// Represents a model's request to invoke a tool.
/// </summary>
public sealed record ToolCall(
    /// <summary>
    /// Provider-assigned identifier for this tool call.
    /// Used to correlate with <see cref="ToolResult.ToolCallId"/>.
    /// </summary>
    string Id,

    /// <summary>
    /// The name of the function to invoke.
    /// </summary>
    string FunctionName,

    /// <summary>
    /// The arguments to pass to the function, as a JSON element.
    /// </summary>
    JsonElement Arguments);
