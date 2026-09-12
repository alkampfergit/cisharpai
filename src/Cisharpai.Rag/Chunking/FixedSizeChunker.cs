using Cisharpai.Rag.Models;

namespace Cisharpai.Rag.Chunking;

/// <summary>
/// Splits exact text slices by Unicode scalar count without breaking valid surrogate pairs.
/// Unpaired surrogates count as one unit and are preserved. Grapheme clusters may span chunks.
/// </summary>
public sealed class FixedSizeChunker : ITextChunker
{
    private readonly FixedSizeChunkerOptions _options;

    public FixedSizeChunker(FixedSizeChunkerOptions? options = null) =>
        _options = (options ?? new()).Snapshot();

    public IEnumerable<TextChunk> Chunk(RagDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Id);
        ArgumentNullException.ThrowIfNull(document.Text);
        return ChunkCore(document);
    }

    private IEnumerable<TextChunk> ChunkCore(RagDocument document)
    {
        var text = document.Text;
        var start = 0;
        var end = Advance(text, 0, _options.ChunkSize);
        var step = _options.ChunkSize - _options.Overlap;
        var index = 0;
        while (start < text.Length)
        {
            yield return new(document.Id, index++, start, text.Substring(start, end - start));
            if (end == text.Length)
                yield break;

            // Moving both boundaries avoids rescanning the overlapping window.
            start = Advance(text, start, step);
            end = Advance(text, end, step);
        }
    }

    private static int Advance(string text, int offset, int count)
    {
        for (var i = 0; i < count && offset < text.Length; i++)
        {
            offset += char.IsHighSurrogate(text[offset])
                && offset + 1 < text.Length && char.IsLowSurrogate(text[offset + 1]) ? 2 : 1;
        }
        return offset;
    }
}
