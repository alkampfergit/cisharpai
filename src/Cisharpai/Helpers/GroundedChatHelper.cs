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
                    ResolveCitedText(content, a.StartIndex, a.EndIndex);
                var (sourceId, sourceData) =
                    ResolveSourceId(a.Filename, a.FileId, a.Index, documents, filenameToDocId);

                return new Citation(
                    Start: citationStart,
                    End: citationEnd,
                    Text: citedText,
                    Sources: [new CitationSource(Id: sourceId, Data: sourceData)],
                    Type: "file_citation");
            })
            .ToList();
    }

    private static (string Text, int Start, int End) ResolveCitedText(
        string content, int? startIndex, int? endIndex)
    {
        if (startIndex.HasValue && endIndex.HasValue
            && endIndex.Value > startIndex.Value
            && endIndex.Value <= content.Length)
        {
            return (content[startIndex.Value..endIndex.Value], startIndex.Value, endIndex.Value);
        }

        return (string.Empty, 0, 0);
    }

    private static (string Id, IReadOnlyDictionary<string, string>? Data) ResolveSourceId(
        string? filename, string? fileId, int? index,
        IReadOnlyList<DocumentChunk> documents, Dictionary<string, string> filenameToDocId)
    {
        // Primary: use index (file ordinal) to look up the document directly
        if (index.HasValue && index.Value >= 0 && index.Value < documents.Count)
        {
            var doc = documents[index.Value];
            return (doc.Id ?? $"document_{index.Value}", doc.Data);
        }

        if (filename is not null && filenameToDocId.TryGetValue(filename, out var docIdByName))
            return (docIdByName, FindDocData(docIdByName, documents));
        if (fileId is not null && filenameToDocId.TryGetValue(fileId, out var docIdByFileId))
            return (docIdByFileId, FindDocData(docIdByFileId, documents));
        return (fileId ?? filename ?? "unknown", null);
    }

    private static IReadOnlyDictionary<string, string>? FindDocData(
        string docId, IReadOnlyList<DocumentChunk> documents)
    {
        for (var i = 0; i < documents.Count; i++)
        {
            if (string.Equals(documents[i].Id, docId, StringComparison.OrdinalIgnoreCase))
                return documents[i].Data;
        }
        return null;
    }
}
