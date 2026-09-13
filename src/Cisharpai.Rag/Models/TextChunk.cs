using System.Collections.ObjectModel;

namespace Cisharpai.Rag.Models;

/// <summary>An exact source slice, identified by document ID and zero-based chunk index.</summary>
/// <param name="DocumentId">The source document identifier.</param>
/// <param name="Index">Zero-based chunk index within the document.</param>
/// <param name="StartOffset">Zero-based UTF-16 offset into the source text, usable with Substring.</param>
/// <param name="EndOffset">Exclusive UTF-16 end offset into the source text (start + slice length for verbatim chunks).</param>
/// <param name="Text">The unmodified source slice.</param>
/// <param name="Metadata">Chunker-specific metadata; empty by default, never null.</param>
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

    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = Metadata ?? EmptyMetadata;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(StartOffset);
        ArgumentOutOfRangeException.ThrowIfLessThan(EndOffset, StartOffset);
    }
}
