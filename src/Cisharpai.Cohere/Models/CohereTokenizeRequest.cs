namespace Cisharpai.Cohere.Models;

internal sealed class CohereTokenizeRequest
{
    public string Text { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}
