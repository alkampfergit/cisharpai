namespace Cisharpai.Models;

/// <summary>
/// A single chunk from a streaming chat completion.
/// </summary>
public sealed record ChatCompletionChunk(
    /// <summary>Content delta text (may be empty for non-content events).</summary>
    string Content,

    /// <summary>Non-null when the stream is finished (e.g. "stop", "tool_calls", "length").</summary>
    string? FinishReason = null,

    /// <summary>Model identifier (typically present on every chunk for OpenAI, first chunk only for Anthropic).</summary>
    string? Model = null,

    /// <summary>Token usage. Only populated on the final chunk when the provider includes it.</summary>
    int? PromptTokens = null,

    /// <summary>Token usage. Only populated on the final chunk when the provider includes it.</summary>
    int? CompletionTokens = null,

    /// <summary>For tool-calling streams: partial tool call deltas.</summary>
    ToolCallDelta? ToolCallDelta = null);

/// <summary>
/// Incremental tool call information emitted during streaming.
/// </summary>
public sealed record ToolCallDelta(
    int Index,
    string? Id,
    string? FunctionName,
    string? ArgumentsDelta);
