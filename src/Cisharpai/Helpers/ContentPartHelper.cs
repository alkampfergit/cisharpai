using System.Text.Json;

namespace Cisharpai.Helpers;

/// <summary>
/// Shared helper for extracting string content from provider response objects.
/// </summary>
public static class ContentPartHelper
{
    /// <summary>
    /// Extracts a string from a content value that may be a raw string or a JsonElement.
    /// Used by OpenAI, Azure OpenAI, and Azure AI Inference providers.
    /// </summary>
    public static string ExtractStringContent(object? content)
    {
        return content switch
        {
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
            _ => string.Empty
        };
    }
}
