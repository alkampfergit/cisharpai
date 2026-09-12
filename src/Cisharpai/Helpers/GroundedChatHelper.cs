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
        Func<TAnnotation, (string Type, string? FileId, string? Filename, int StartIndex, int EndIndex)> extractor)
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
                var citedText = a.StartIndex >= 0 && a.EndIndex <= content.Length && a.StartIndex < a.EndIndex
                    ? content[a.StartIndex..a.EndIndex]
                    : string.Empty;

                var sourceId = a.Filename is not null && filenameToDocId.TryGetValue(a.Filename, out var docId)
                    ? docId
                    : a.FileId ?? a.Filename ?? "unknown";

                return new Citation(
                    Start: a.StartIndex,
                    End: a.EndIndex,
                    Text: citedText,
                    Sources: [new CitationSource(Id: sourceId)],
                    Type: "file_citation");
            })
            .ToList();
    }
}
