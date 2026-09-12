using System.Text.Json;

namespace Cisharpai.Models;

/// <summary>
/// Defines a tool (function) that a model can invoke.
/// </summary>
public sealed record ToolDefinition(
    /// <summary>
    /// The name of the function. Must be a valid identifier.
    /// </summary>
    string Name,

    /// <summary>
    /// A human-readable description of what the function does.
    /// </summary>
    string Description,

    /// <summary>
    /// JSON Schema describing the function's parameters.
    /// </summary>
    JsonElement Parameters,

    /// <summary>
    /// When true, enables strict schema enforcement (OpenAI Structured Outputs for tools).
    /// Defaults to true.
    /// </summary>
    bool Strict = true)
{
    /// <summary>
    /// Validates the tool definition. Throws <see cref="ArgumentException"/> if Name is missing
    /// or Parameters is not a JSON object.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException(
                "Tool name is required.", nameof(Name));

        if (Parameters.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(
                "Parameters must be a JSON object (JSON Schema).", nameof(Parameters));
    }
}
