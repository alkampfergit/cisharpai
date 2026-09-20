using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureOpenAi.Models;

public sealed class AzureOpenAiToolDefinition
{
    public string Type { get; set; } = "function";

    public AzureOpenAiToolFunction Function { get; set; } = new();
}

public sealed class AzureOpenAiToolFunction
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public JsonElement? Parameters { get; set; }

    public bool? Strict { get; set; }
}

public sealed class AzureOpenAiToolChoiceFunction
{
    public string Name { get; set; } = string.Empty;
}

public sealed class AzureOpenAiToolChoiceObject
{
    public string Type { get; set; } = "function";

    public AzureOpenAiToolChoiceFunction Function { get; set; } = new();
}

public sealed class AzureOpenAiToolCallFunction
{
    public string Name { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;
}

public sealed class AzureOpenAiToolCall
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = "function";

    public AzureOpenAiToolCallFunction Function { get; set; } = new();
}
