using System.Text.Json.Serialization;

namespace Cisharpai.Anthropic.Models;

/// <summary>
/// A streaming event from the Anthropic API.
/// The event type is in the "type" field of the JSON data payload.
/// </summary>
public sealed class AnthropicStreamEvent
{
    public string? Type { get; set; }

    public int? Index { get; set; }

    public AnthropicStreamDelta? Delta { get; set; }

    public AnthropicUsage? Usage { get; set; }

    /// <summary>For message_start events: the initial message object with model/usage info.</summary>
    public AnthropicStreamMessage? Message { get; set; }
}

/// <summary>
/// The delta object in an Anthropic content_block_delta or message_delta event.
/// </summary>
public sealed class AnthropicStreamDelta
{
    public string? Type { get; set; }

    public string? Text { get; set; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}

/// <summary>
/// The initial message metadata in an Anthropic message_start event.
/// </summary>
public sealed class AnthropicStreamMessage
{
    public string? Model { get; set; }

    public AnthropicUsage? Usage { get; set; }
}
