using System.Text.Json;

namespace Cisharpai.Models;

/// <summary>
/// Configures JSON output behavior for chat completions.
/// </summary>
public sealed record JsonOutputOptions(
    /// <summary>
    /// Whether to use JsonMode (json_object) or JsonSchema (json_schema).
    /// </summary>
    JsonOutputMode Mode,

    /// <summary>
    /// Required when Mode == JsonSchema. The name for the schema (used by OpenAI for caching/identification).
    /// Must be a valid identifier.
    /// </summary>
    string? SchemaName = null,

    /// <summary>
    /// Optional. A human-readable description of what the schema represents.
    /// </summary>
    string? SchemaDescription = null,

    /// <summary>
    /// Required when Mode == JsonSchema. The JSON schema string as provided by the caller.
    /// Schema construction and correctness is caller-owned.
    /// </summary>
    string? JsonSchema = null,

    /// <summary>
    /// When true and Mode == JsonSchema, enables strict schema enforcement (OpenAI Structured Outputs).
    /// When false, the schema is advisory only. Defaults to true.
    /// </summary>
    bool Strict = true)
{
    /// <summary>
    /// Validates the options. Throws <see cref="ArgumentException"/> if Mode is JsonSchema
    /// but SchemaName or JsonSchema are missing.
    /// </summary>
    public void Validate()
    {
        if (Mode != JsonOutputMode.JsonSchema)
            return;

        if (string.IsNullOrWhiteSpace(SchemaName))
            throw new ArgumentException(
                "SchemaName is required when Mode is JsonSchema.", nameof(SchemaName));

        if (string.IsNullOrWhiteSpace(JsonSchema))
            throw new ArgumentException(
                "JsonSchema is required when Mode is JsonSchema.", nameof(JsonSchema));

        try
        {
            using var doc = JsonDocument.Parse(JsonSchema);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"JsonSchema contains invalid JSON: {JsonSchema}. Parse error: {ex.Message}",
                nameof(JsonSchema));
        }
    }
}
