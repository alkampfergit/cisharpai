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
        Func<TAnnotation, (string Type, string? FileId, string? Filename, int? Index, int StartIndex, int EndIndex)> extractor)
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
                var hasOffsets = a.StartIndex >= 0 && a.EndIndex > a.StartIndex && a.EndIndex <= content.Length;
                var citedText = hasOffsets
                    ? content[a.StartIndex..a.EndIndex]
                    : string.Empty;

                var citationStart = hasOffsets ? a.StartIndex : (a.Index ?? 0);
                var citationEnd = hasOffsets ? a.EndIndex : (a.Index ?? 0);

                string sourceId;
                if (a.Filename is not null && filenameToDocId.TryGetValue(a.Filename, out var docIdByName))
                    sourceId = docIdByName;
                else if (a.FileId is not null && filenameToDocId.TryGetValue(a.FileId, out var docIdByFileId))
                    sourceId = docIdByFileId;
                else
                    sourceId = a.FileId ?? a.Filename ?? "unknown";

                return new Citation(
                    Start: citationStart,
                    End: citationEnd,
                    Text: citedText,
                    Sources: [new CitationSource(Id: sourceId)],
                    Type: "file_citation");
            })
            .ToList();
    }
}
