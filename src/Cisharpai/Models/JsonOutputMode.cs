namespace Cisharpai.Models;

/// <summary>
/// Specifies the JSON output enforcement mode for chat completions.
/// </summary>
public enum JsonOutputMode
{
    /// <summary>
    /// Forces the model to produce valid JSON output without schema enforcement.
    /// Maps to response_format type 'json_object'.
    /// Requires the word 'JSON' in the system message for OpenAI models.
    /// Supported by all model families.
    /// </summary>
    JsonMode,

    /// <summary>
    /// Forces the model to produce output conforming to a caller-supplied JSON Schema.
    /// Maps to response_format type 'json_schema' with strict enforcement.
    /// Only supported on gpt-4o-2024-08-06+, gpt-4.1, o-series, and gpt-5 class models.
    /// </summary>
    JsonSchema
}
