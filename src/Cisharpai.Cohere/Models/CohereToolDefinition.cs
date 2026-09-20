using System.Text.Json;

namespace Cisharpai.Cohere.Models;

public sealed class CohereToolDefinition
{
    public string Type { get; set; } = "function";

    public CohereToolFunction Function { get; set; } = new();
}

public sealed class CohereToolFunction
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public JsonElement? Parameters { get; set; }
}

public sealed class CohereToolCallFunction
{
    public string Name { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;
}

public sealed class CohereToolCall
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = "function";

    public CohereToolCallFunction Function { get; set; } = new();
}
