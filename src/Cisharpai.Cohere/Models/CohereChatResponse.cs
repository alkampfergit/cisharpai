namespace Cisharpai.Cohere.Models;

public sealed class CohereChatContentBlock
{
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class CohereChatResponseMessage
{
    public string Role { get; set; } = string.Empty;
    public List<CohereChatContentBlock> Content { get; set; } = [];
    public List<CohereChatCitation>? Citations { get; set; }
    public List<CohereToolCall>? ToolCalls { get; set; }
}

public sealed class CohereChatTokens
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

public sealed class CohereChatBilledUnits
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

public sealed class CohereChatUsage
{
    public CohereChatBilledUnits BilledUnits { get; set; } = new();
    public CohereChatTokens Tokens { get; set; } = new();
}

public sealed class CohereChatResponse
{
    public string Id { get; set; } = string.Empty;
    public string FinishReason { get; set; } = string.Empty;
    public CohereChatResponseMessage Message { get; set; } = new();
    public CohereChatUsage Usage { get; set; } = new();
}
