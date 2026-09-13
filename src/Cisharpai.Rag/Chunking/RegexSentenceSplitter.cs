using System.Text.RegularExpressions;

namespace Cisharpai.Rag.Chunking;

/// <summary>
/// Splits text on sentence-ending punctuation (<c>.</c> <c>!</c> <c>?</c>) followed by
/// whitespace or end-of-string. Targets English prose and will mis-split on abbreviations
/// like "Dr." and "e.g." — inject a custom <see cref="ISentenceSplitter"/> for better accuracy.
/// </summary>
public sealed partial class RegexSentenceSplitter : ISentenceSplitter
{
    [GeneratedRegex(@"(?<=[.!?])\s+", RegexOptions.Compiled)]
    private static partial Regex SentenceBoundary();

    public IReadOnlyList<string> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return SentenceBoundary().Split(text)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();
    }
}
