namespace Cisharpai.Cohere.Models;

public sealed class CohereChatCitationSource
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, string>? Document { get; set; }
}

public sealed class CohereChatCitation
{
    public int Start { get; set; }
    public int End { get; set; }
    public string Text { get; set; } = string.Empty;
    public List<CohereChatCitationSource> Sources { get; set; } = [];
    public string? Type { get; set; }
}
