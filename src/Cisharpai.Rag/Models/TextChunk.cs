using System.Collections.ObjectModel;

namespace Cisharpai.Rag.Models;

/// <summary>
/// A chunk of text from a source document, identified by document ID and zero-based chunk index.
/// For locally-chunked text, <c>StartOffset</c> and <c>EndOffset</c> delimit the original source
/// span in UTF-16 offsets. For hosted retrieval (e.g. OpenAI <c>file_search</c>), offsets are
/// passage-relative: <c>StartOffset</c> is <c>0</c> and <c>EndOffset</c> is <c>Text.Length</c>,
/// because the provider chunked the file and returns a passage whose position in the original
/// document is unknown. Do not assume offsets from different retrieval sources are comparable.
/// <c>Text</c> is the verbatim source slice for all built-in chunkers; future non-verbatim chunkers
/// (e.g. contextual retrieval) may produce text that differs from the source span.
/// </summary>
/// <param name="DocumentId">The source document identifier.</param>
/// <param name="Index">Zero-based chunk index within the document.</param>
/// <param name="StartOffset">Zero-based UTF-16 offset into the source text for locally-chunked text. For hosted retrieval, this is <c>0</c> (passage-relative).</param>
/// <param name="EndOffset">Exclusive UTF-16 end offset into the source text for locally-chunked text. For hosted retrieval, this is <c>Text.Length</c> (passage-relative).</param>
/// <param name="Text">The chunk text. For built-in chunkers this is the unmodified source slice; non-verbatim chunkers may prepend or modify content.</param>
/// <param name="Metadata">Chunker-specific metadata; defensively copied on construction, empty by default, never null.</param>
public sealed record TextChunk(
    string DocumentId,
    int Index,
    int StartOffset,
    int EndOffset,
    string Text,
    IReadOnlyDictionary<string, object?> Metadata)
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyMetadata =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    public TextChunk(string DocumentId, int Index, int StartOffset, int EndOffset, string Text)
        : this(DocumentId, Index, StartOffset, EndOffset, Text, EmptyMetadata)
    {
    }

    private readonly IReadOnlyDictionary<string, object?> _metadata = NormalizeMetadata(Metadata);

    public IReadOnlyDictionary<string, object?> Metadata
    {
        get => _metadata;
        init => _metadata = NormalizeMetadata(value);
    }

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(StartOffset);
        ArgumentOutOfRangeException.ThrowIfLessThan(EndOffset, StartOffset);
    }

    private static IReadOnlyDictionary<string, object?> NormalizeMetadata(IReadOnlyDictionary<string, object?>? value)
    {
        if (value is null || value.Count == 0) return EmptyMetadata;
        return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(value));
    }
}
