using System.Text;
using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Helpers;

public static class GroundedChatHelper
{
    public static (string Filename, string FileData) EncodeDocumentChunk(DocumentChunk doc, int index)
    {
        var content = doc.Text ?? JsonSerializer.Serialize(doc.Data!);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        return (
            doc.Id ?? $"document_{index}.txt",
            $"data:text/plain;base64,{base64}");
    }

    public static List<Citation> MapAnnotationsToCitations<TAnnotation>(
        List<TAnnotation> annotations,
        string content,
        IReadOnlyList<DocumentChunk> documents,
        Func<TAnnotation, (string Type, string? FileId, string? Filename, int? Index, int? StartIndex, int? EndIndex)> extractor)
    {
        if (annotations.Count == 0)
            return [];

        var filenameToDocId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < documents.Count; i++)
        {
            var filename = documents[i].Id ?? $"document_{i}.txt";
            var docId = documents[i].Id ?? $"document_{i}";
            filenameToDocId[filename] = docId;
        }

        return annotations
            .Select(extractor)
            .Where(a => a.Type == "file_citation")
            .Select(a =>
            {
                var (citedText, citationStart, citationEnd) =
                    ResolveCitedText(content, a.StartIndex, a.EndIndex, a.Index);
                var sourceId = ResolveSourceId(a.Filename, a.FileId, filenameToDocId);

                return new Citation(
                    Start: citationStart,
                    End: citationEnd,
                    Text: citedText,
                    Sources: [new CitationSource(Id: sourceId)],
                    Type: "file_citation");
            })
            .ToList();
    }

    private static (string Text, int Start, int End) ResolveCitedText(
        string content, int? startIndex, int? endIndex, int? index)
    {
        if (startIndex.HasValue && endIndex.HasValue
            && endIndex.Value > startIndex.Value
            && endIndex.Value <= content.Length)
        {
            return (content[startIndex.Value..endIndex.Value], startIndex.Value, endIndex.Value);
        }

        if (index is { } idx && idx >= 0 && idx < content.Length)
        {
            var wordEnd = idx;
            while (wordEnd < content.Length && !char.IsWhiteSpace(content[wordEnd]) && content[wordEnd] != '.')
                wordEnd++;
            return (content[idx..wordEnd], idx, wordEnd);
        }

        return (string.Empty, 0, 0);
    }

    private static string ResolveSourceId(
        string? filename, string? fileId, Dictionary<string, string> filenameToDocId)
    {
        if (filename is not null && filenameToDocId.TryGetValue(filename, out var docIdByName))
            return docIdByName;
        if (fileId is not null && filenameToDocId.TryGetValue(fileId, out var docIdByFileId))
            return docIdByFileId;
        return fileId ?? filename ?? "unknown";
    }
}
