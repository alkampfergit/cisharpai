using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cisharpai.Models;

namespace Cisharpai.Helpers;

public static class GroundedChatFallbackHelper
{
    private static readonly Regex MarkerPattern = new(
        @"«cite:(\d+)»(.*?)«/cite»",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static IReadOnlyList<LlmMessage> BuildGroundingMessages(
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<DocumentChunk> documents)
    {
        var contextBlock = BuildContextBlock(documents);
        var instruction = BuildInstruction();

        var groundingContent = contextBlock + "\n\n" + instruction;

        var result = new List<LlmMessage>(messages.Count + 1);

        var existingSystem = messages.FirstOrDefault(m => m.Role == LlmRole.System);
        if (existingSystem is not null)
        {
            foreach (var m in messages)
            {
                if (m.Role == LlmRole.System)
                    result.Add(new LlmMessage(LlmRole.System, m.Content + "\n\n" + groundingContent));
                else
                    result.Add(m);
            }
        }
        else
        {
            result.Add(new LlmMessage(LlmRole.System, groundingContent));
            result.AddRange(messages);
        }

        return result;
    }

    public static (string CleanContent, IReadOnlyList<Citation> Citations) ParseAndStripMarkers(
        string rawContent,
        IReadOnlyList<DocumentChunk> documents)
    {
        if (string.IsNullOrEmpty(rawContent))
            return (rawContent ?? string.Empty, Array.Empty<Citation>());

        var matches = MarkerPattern.Matches(rawContent);
        if (matches.Count == 0)
            return (rawContent, Array.Empty<Citation>());

        var citations = new List<Citation>();
        var cleanBuilder = new StringBuilder(rawContent.Length);
        var lastEnd = 0;

        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Groups[1].Value, out var docIndex))
                continue;

            cleanBuilder.Append(rawContent, lastEnd, match.Index - lastEnd);

            var citedText = match.Groups[2].Value;
            var cleanStart = cleanBuilder.Length;
            cleanBuilder.Append(citedText);
            var cleanEnd = cleanBuilder.Length;

            var (sourceId, sourceData) = ResolveSource(docIndex, documents);

            citations.Add(new Citation(
                Start: cleanStart,
                End: cleanEnd,
                Text: citedText,
                Sources: [new CitationSource(Id: sourceId, Data: sourceData)],
                Type: "synthesized_citation"));

            lastEnd = match.Index + match.Length;
        }

        cleanBuilder.Append(rawContent, lastEnd, rawContent.Length - lastEnd);

        return (cleanBuilder.ToString(), citations);
    }

    private static string BuildContextBlock(IReadOnlyList<DocumentChunk> documents)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== REFERENCE DOCUMENTS ===");

        for (var i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            var docId = doc.Id ?? $"document_{i}";
            var content = string.IsNullOrWhiteSpace(doc.Text)
                ? JsonSerializer.Serialize(doc.Data!)
                : doc.Text;

            sb.AppendLine($"[Document {i}: {docId}]");
            sb.AppendLine(content);
            sb.AppendLine();
        }

        sb.AppendLine("=== END REFERENCE DOCUMENTS ===");
        return sb.ToString();
    }

    private static string BuildInstruction()
    {
        return """
            When answering, cite the reference documents by wrapping the cited text in markers: «cite:N»cited text«/cite» where N is the document index number (0-based). Only cite from the provided documents. Do not nest citation markers. If no documents are relevant, answer without markers.
            """.Trim();
    }

    private static (string Id, ReadOnlyDictionary<string, string>? Data) ResolveSource(
        int docIndex,
        IReadOnlyList<DocumentChunk> documents)
    {
        if (docIndex < 0 || docIndex >= documents.Count)
            return ($"document_{docIndex}", null);

        var doc = documents[docIndex];
        var id = doc.Id ?? $"document_{docIndex}";
        var data = doc.Data is not null
            ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(doc.Data))
            : null;

        return (id, data);
    }
}
