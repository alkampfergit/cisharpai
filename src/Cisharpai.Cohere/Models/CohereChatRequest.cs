namespace Cisharpai.Cohere.Models;

public sealed class CohereChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? ToolCallId { get; set; }
    public List<CohereToolCall>? ToolCalls { get; set; }
}

public sealed class CohereChatRequest
{
    public string Model { get; set; } = string.Empty;
    public List<CohereChatMessage> Messages { get; set; } = [];
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public CohereChatResponseFormat? ResponseFormat { get; set; }
    public List<CohereChatDocument>? Documents { get; set; }
    public CohereCitationOptions? CitationOptions { get; set; }
    public List<CohereToolDefinition>? Tools { get; set; }
    public string? ToolChoice { get; set; }
    public bool? StrictTools { get; set; }
    public bool? Stream { get; set; }
}
