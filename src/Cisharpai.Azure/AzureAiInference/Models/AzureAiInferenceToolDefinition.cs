using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cisharpai.Azure.AzureAiInference.Models;

public sealed class AzureAiInferenceToolDefinition
{
    public string Type { get; set; } = "function";

    public AzureAiInferenceToolFunction Function { get; set; } = new();
}

public sealed class AzureAiInferenceToolFunction
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public JsonElement? Parameters { get; set; }

    public bool? Strict { get; set; }
}

public sealed class AzureAiInferenceToolChoiceFunction
{
    public string Name { get; set; } = string.Empty;
}

public sealed class AzureAiInferenceToolChoiceObject
{
    public string Type { get; set; } = "function";

    public AzureAiInferenceToolChoiceFunction Function { get; set; } = new();
}

public sealed class AzureAiInferenceToolCallFunction
{
    public string Name { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;
}

public sealed class AzureAiInferenceToolCall
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = "function";

    public AzureAiInferenceToolCallFunction Function { get; set; } = new();
}
