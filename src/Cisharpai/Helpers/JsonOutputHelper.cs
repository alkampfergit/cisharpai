using Cisharpai.Models;

namespace Cisharpai.Helpers;

/// <summary>
/// Shared helper methods for JSON output handling across providers.
/// </summary>
public static class JsonOutputHelper
{
    private const string DefaultSuffix = "Respond in JSON.";

    /// <summary>
    /// Ensures that the system message contains the word "JSON" when using JsonMode.
    /// If the system message doesn't already mention JSON, appends the given suffix.
    /// </summary>
    public static IReadOnlyList<LlmMessage> EnsureJsonKeywordInSystemMessage(
        IReadOnlyList<LlmMessage> messages,
        JsonOutputOptions options,
        string suffix = DefaultSuffix)
    {
        if (options.Mode != JsonOutputMode.JsonMode)
            return messages;

        var systemMessage = messages.FirstOrDefault(m => m.Role == LlmRole.System);

        if (systemMessage is not null &&
            systemMessage.Content.Contains("JSON", StringComparison.OrdinalIgnoreCase))
            return messages;

        var result = new List<LlmMessage>(messages);

        if (systemMessage is not null)
        {
            var index = result.IndexOf(systemMessage);
            result[index] = new LlmMessage(LlmRole.System, systemMessage.Content + " " + suffix);
        }
        else
        {
            result.Insert(0, new LlmMessage(LlmRole.System, suffix));
        }

        return result;
    }

    /// <summary>
    /// Strips markdown code fences (```json ... ``` or ``` ... ```) from content.
    /// Used by providers that don't have native JSON mode and may wrap output in markdown.
    /// </summary>
    public static string StripMarkdownCodeFences(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return content;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
            return content;

        trimmed = trimmed[(firstNewline + 1)..];

        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFence >= 0)
            trimmed = trimmed[..lastFence];

        return trimmed.Trim();
    }
}
