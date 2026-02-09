namespace Cisharpai.Cohere.Models;

public sealed class CohereChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class CohereChatRequest
{
    public string Model { get; set; } = string.Empty;
    public List<CohereChatMessage> Messages { get; set; } = [];
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public CohereChatResponseFormat? ResponseFormat { get; set; }
}
