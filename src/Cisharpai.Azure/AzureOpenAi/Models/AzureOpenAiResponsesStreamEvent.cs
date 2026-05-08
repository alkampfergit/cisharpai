namespace Cisharpai.Azure.AzureOpenAi.Models;

public sealed class AzureOpenAiResponsesStreamEvent
{
    public string? Type { get; set; }

    /// <summary>Text delta for response.output_text.delta events.</summary>
    public string? Delta { get; set; }

    /// <summary>Full response object for response.completed events.</summary>
    public AzureOpenAiResponsesApiResponse? Response { get; set; }
}
