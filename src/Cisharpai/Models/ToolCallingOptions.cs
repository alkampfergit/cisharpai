namespace Cisharpai.Models;

/// <summary>
/// Configures tool calling behavior for chat completions.
/// </summary>
public sealed record ToolCallingOptions(
    /// <summary>
    /// The tool definitions to offer to the model.
    /// </summary>
    IReadOnlyList<ToolDefinition> Tools,

    /// <summary>
    /// Controls how the model selects which tool to call.
    /// When null, defaults to Auto behavior.
    /// </summary>
    ToolChoice? ToolChoice = null)
{
    /// <summary>
    /// Validates the options. Throws <see cref="ArgumentException"/> if Tools is null or empty,
    /// or if any individual <see cref="ToolDefinition"/> is invalid.
    /// </summary>
    public void Validate()
    {
        if (Tools is null || Tools.Count == 0)
            throw new ArgumentException(
                "At least one tool definition is required.", nameof(Tools));

        foreach (var tool in Tools)
            tool.Validate();
    }
}
