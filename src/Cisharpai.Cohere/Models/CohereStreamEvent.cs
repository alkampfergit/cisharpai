using System.Text.Json.Serialization;

namespace Cisharpai.Cohere.Models;

/// <summary>
/// A streaming event from the Cohere Chat API.
/// </summary>
public sealed class CohereStreamEvent
{
    public string? Type { get; set; }

    public CohereStreamDelta? Delta { get; set; }
}

public sealed class CohereStreamDelta
{
    public CohereStreamMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    public CohereStreamUsage? Usage { get; set; }
}

public sealed class CohereStreamMessage
{
    public CohereStreamContent? Content { get; set; }
}

public sealed class CohereStreamContent
{
    public string? Text { get; set; }
}

public sealed class CohereStreamUsage
{
    [JsonPropertyName("billed_units")]
    public CohereStreamBilledUnits? BilledUnits { get; set; }
}

public sealed class CohereStreamBilledUnits
{
    [JsonPropertyName("input_tokens")]
    public int? InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int? OutputTokens { get; set; }
}
