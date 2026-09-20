using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiToolDefinition
{
    public string Type { get; set; } = "function";

    public OpenAiToolFunction Function { get; set; } = new();
}

public sealed class OpenAiToolFunction
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public JsonElement? Parameters { get; set; }

    public bool? Strict { get; set; }
}

public sealed class OpenAiToolChoiceFunction
{
    public string Name { get; set; } = string.Empty;
}

public sealed class OpenAiToolChoiceObject
{
    public string Type { get; set; } = "function";

    public OpenAiToolChoiceFunction Function { get; set; } = new();
}

public sealed class OpenAiToolCallFunction
{
    public string Name { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;
}

public sealed class OpenAiToolCall
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = "function";

    public OpenAiToolCallFunction Function { get; set; } = new();
}
