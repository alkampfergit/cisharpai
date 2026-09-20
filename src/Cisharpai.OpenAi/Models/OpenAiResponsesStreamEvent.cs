using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

/// <summary>
/// A streaming event from the OpenAI Responses API (GPT-5).
/// The event type is in the "type" field of the JSON data payload.
/// </summary>
public sealed class OpenAiResponsesStreamEvent
{
    public string? Type { get; set; }

    /// <summary>Text delta for response.output_text.delta events.</summary>
    public string? Delta { get; set; }

    /// <summary>Full response object for response.completed events.</summary>
    public OpenAiResponsesApiResponse? Response { get; set; }
}
