namespace Cisharpai.Cohere.Models;

internal sealed class CohereTokenizeResponse
{
    public int[] Tokens { get; set; } = [];
    public string[] TokenStrings { get; set; } = [];
}
