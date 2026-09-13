using System.Runtime.CompilerServices;
using Cisharpai.Rag.Models;

namespace Cisharpai.Rag.Chunking;

/// <summary>
/// Splits text along structural boundaries (paragraph, sentence, word) before resorting to a
/// hard cut, producing chunks that respect natural language boundaries whenever possible.
/// <para>
/// The separator ladder is tried in order: the chunker splits by the first separator, and for
/// any piece that still exceeds the budget, falls through to the next. An empty-string terminal
/// separator performs a hard character- or token-level cut, guaranteeing that <b>every</b> emitted
/// chunk fits within <see cref="RecursiveChunkerOptions.MaxChunkSize"/>.
/// </para>
/// <para>
/// <b>Sizing modes:</b> by default, sizes count Unicode scalar values (not tokens).
/// A supplementary character (e.g. an emoji) counts as one scalar, regardless of how many
/// UTF-16 code units it occupies. Set <see cref="RecursiveChunkerOptions.TokenCounter"/>
/// to measure in real tokens instead. The token counter does not force a dependency on
/// <c>Cisharpai.Rag.Tokenizers</c> — consumers who only want character-based sizing never
/// install it.
/// </para>
/// <para>
/// <b>Overlap:</b> consecutive chunks share <see cref="RecursiveChunkerOptions.ChunkOverlap"/>
/// units (Unicode scalars or tokens) of trailing/leading text so that context is preserved
/// across chunk boundaries. The overlap never causes a chunk to exceed
/// <see cref="RecursiveChunkerOptions.MaxChunkSize"/>.
/// </para>
/// </summary>
/// <remarks>
/// The current implementation materialises all raw chunks and applies overlap before yielding
/// the first result. It is not a lazy streaming pipeline.
/// </remarks>
public sealed class RecursiveChunker : ITextChunker
{
    private readonly RecursiveChunkerOptions _options;

    public RecursiveChunker(RecursiveChunkerOptions? options = null)
    {
        _options = (options ?? new RecursiveChunkerOptions()).Snapshot();
    }

    public IAsyncEnumerable<TextChunk> ChunkAsync(
        RagDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Id);
        ArgumentNullException.ThrowIfNull(document.Text);
        return ChunkCoreAsync(document, cancellationToken);
    }

    private async IAsyncEnumerable<TextChunk> ChunkCoreAsync(
        RagDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var text = document.Text;
        if (text.Length == 0)
            yield break;

        var totalSize = await MeasureSizeAsync(text, 0, text.Length, cancellationToken)
            .ConfigureAwait(false);
        if (totalSize <= _options.MaxChunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new TextChunk(document.Id, 0, 0, text.Length, text);
            yield break;
        }

        var rawChunks = new List<(int Start, int End, bool Oversized)>();
        await SplitRegionAsync(text, 0, text.Length, 0, rawChunks, cancellationToken)
            .ConfigureAwait(false);

        var chunks = ApplyOverlap(text, rawChunks);

        for (var i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (start, end) = chunks[i];
            yield return new TextChunk(document.Id, i, start, end, text[start..end]);
        }
    }

    private async Task SplitRegionAsync(
        string sourceText,
        int regionStart,
        int regionEnd,
        int separatorIndex,
        List<(int Start, int End, bool Oversized)> result,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (regionEnd <= regionStart)
            return;

        var regionSize = await MeasureSizeAsync(sourceText, regionStart, regionEnd, ct)
            .ConfigureAwait(false);
        if (regionSize <= _options.MaxChunkSize)
        {
            result.Add((regionStart, regionEnd, false));
            return;
        }

        var separators = _options.Separators!;

        if (separatorIndex >= separators.Count)
        {
            result.Add((regionStart, regionEnd, true));
            return;
        }

        var sep = separators[separatorIndex];

        if (sep.Length == 0)
        {
            await HardCutAsync(sourceText, regionStart, regionEnd, result, ct)
                .ConfigureAwait(false);
            return;
        }

        var contentStarts = FindContentStarts(sourceText, regionStart, regionEnd, sep);

        if (contentStarts.Count <= 1)
        {
            await SplitRegionAsync(sourceText, regionStart, regionEnd, separatorIndex + 1, result, ct)
                .ConfigureAwait(false);
            return;
        }

        await MergePiecesAsync(sourceText, contentStarts, regionEnd, separatorIndex, result, ct)
            .ConfigureAwait(false);
    }

    private async Task MergePiecesAsync(
        string sourceText,
        List<int> contentStarts,
        int regionEnd,
        int separatorIndex,
        List<(int Start, int End, bool Oversized)> result,
        CancellationToken ct)
    {
        var numPieces = contentStarts.Count;
        var chunkFrom = 0;

        for (var i = 0; i < numPieces; i++)
        {
            ct.ThrowIfCancellationRequested();

            var candidateEnd = PieceEnd(contentStarts, i, regionEnd);
            var candidateSize = await MeasureSizeAsync(sourceText, contentStarts[chunkFrom], candidateEnd, ct)
                .ConfigureAwait(false);

            if (candidateSize <= _options.MaxChunkSize)
                continue;

            if (i > chunkFrom)
            {
                result.Add((contentStarts[chunkFrom], contentStarts[i], false));
                chunkFrom = i;
            }

            var pieceEnd = PieceEnd(contentStarts, i, regionEnd);
            var pieceSize = await MeasureSizeAsync(sourceText, contentStarts[i], pieceEnd, ct)
                .ConfigureAwait(false);

            if (pieceSize > _options.MaxChunkSize)
            {
                await SplitRegionAsync(sourceText, contentStarts[i], pieceEnd, separatorIndex + 1, result, ct)
                    .ConfigureAwait(false);
                chunkFrom = i + 1;
            }
        }

        if (chunkFrom < numPieces && contentStarts[chunkFrom] < regionEnd)
            result.Add((contentStarts[chunkFrom], regionEnd, false));
    }

    private Task HardCutAsync(
        string sourceText,
        int regionStart,
        int regionEnd,
        List<(int Start, int End, bool Oversized)> result,
        CancellationToken ct)
    {
        var strideSize = _options.ChunkOverlap > 0
            ? _options.MaxChunkSize - _options.ChunkOverlap
            : _options.MaxChunkSize;

        var pos = regionStart;
        while (pos < regionEnd)
        {
            ct.ThrowIfCancellationRequested();

            var (cutEnd, isRemainder) = ComputeHardCutEnd(sourceText, pos, regionEnd, strideSize);
            if (isRemainder)
            {
                result.Add((pos, regionEnd, false));
                break;
            }

            cutEnd = Math.Min(cutEnd, regionEnd);

            if (cutEnd <= pos)
                cutEnd = AdvancePastCodePoint(sourceText, pos);

            cutEnd = AdjustSurrogates(sourceText, cutEnd, regionEnd);
            result.Add((pos, cutEnd, false));
            pos = cutEnd;
        }

        return Task.CompletedTask;
    }

    private (int CutEnd, bool IsRemainder) ComputeHardCutEnd(
        string sourceText, int pos, int regionEnd, int strideSize)
    {
        if (_options.TokenCounter != null)
        {
            if (_options.TokenSlicerFromStart == null)
                throw new InvalidOperationException(
                    "Token-based sizing requires RecursiveChunkerOptions.TokenSlicerFromStart " +
                    "to handle oversized text that cannot be split by any separator. " +
                    "Use TiktokenCounter.ToTokenSlicerFromStart() or provide a custom delegate.");

            var remainingLen = regionEnd - pos;
            var boundedLen = Math.Min(remainingLen, strideSize * 8);
            var slice = sourceText.Substring(pos, boundedLen);
            var sliceEnd = _options.TokenSlicerFromStart(slice, strideSize);

            if (sliceEnd >= slice.Length)
            {
                if (boundedLen >= remainingLen)
                    return (regionEnd, true);

                slice = sourceText.Substring(pos, remainingLen);
                sliceEnd = _options.TokenSlicerFromStart(slice, strideSize);
                if (sliceEnd >= slice.Length)
                    return (regionEnd, true);
            }

            return (pos + sliceEnd, false);
        }

        var advanceFull = pos + AdvanceScalars(sourceText, pos, _options.MaxChunkSize);
        if (advanceFull >= regionEnd)
            return (regionEnd, true);

        return (pos + AdvanceScalars(sourceText, pos, strideSize), false);
    }

    private List<(int Start, int End)> ApplyOverlap(
        string sourceText,
        List<(int Start, int End, bool Oversized)> rawChunks)
    {
        if (_options.ChunkOverlap == 0 || rawChunks.Count <= 1)
            return rawChunks.ConvertAll(c => (c.Start, c.End));

        var result = new List<(int Start, int End)>(rawChunks.Count);
        result.Add((rawChunks[0].Start, rawChunks[0].End));

        for (var i = 1; i < rawChunks.Count; i++)
        {
            var overlapStart = ComputeOverlapStart(
                sourceText, rawChunks[i - 1], rawChunks[i]);
            result.Add((overlapStart, rawChunks[i].End));
        }

        return result;
    }

    private int ComputeOverlapStart(
        string sourceText,
        (int Start, int End, bool Oversized) prev,
        (int Start, int End, bool Oversized) chunk)
    {
        int overlapStart;
        if (_options.TokenCounter != null)
        {
            var prevText = sourceText[prev.Start..prev.End];
            overlapStart = prev.Start + _options.TokenSlicerFromEnd!(prevText, _options.ChunkOverlap);
        }
        else
        {
            overlapStart = RetreatScalars(sourceText, prev.Start, prev.End, _options.ChunkOverlap);
        }

        overlapStart = Math.Max(overlapStart, prev.Start);
        overlapStart = Math.Min(overlapStart, chunk.Start);
        overlapStart = AdjustSurrogateStart(sourceText, overlapStart, prev.Start);

        if (!chunk.Oversized)
        {
            overlapStart = CapOverlapToBudget(sourceText, overlapStart, prev.Start, chunk.End);
            overlapStart = Math.Min(overlapStart, chunk.Start);
            overlapStart = AdjustSurrogateStart(sourceText, overlapStart, prev.Start);
        }

        return overlapStart;
    }

    private int CapOverlapToBudget(
        string sourceText, int overlapStart, int lowerBound, int chunkEnd)
    {
        if (_options.TokenCounter != null)
        {
            var overlappedText = sourceText[overlapStart..chunkEnd];
            var capIndex = _options.TokenSlicerFromEnd!(overlappedText, _options.MaxChunkSize);
            var cappedStart = overlapStart + capIndex;
            if (cappedStart > overlapStart)
            {
                overlapStart = cappedStart;
                overlapStart = AdjustSurrogateStart(sourceText, overlapStart, lowerBound);
            }
        }
        else
        {
            var maxStart = RetreatScalars(sourceText, 0, chunkEnd, _options.MaxChunkSize);
            if (overlapStart < maxStart)
            {
                overlapStart = maxStart;
                overlapStart = AdjustSurrogateStart(sourceText, overlapStart, lowerBound);
            }
        }

        return overlapStart;
    }

    private ValueTask<int> MeasureSizeAsync(string sourceText, int start, int end, CancellationToken ct)
    {
        if (_options.TokenCounter == null)
            return new ValueTask<int>(end - start);
        return _options.TokenCounter.CountAsync(sourceText[start..end], ct);
    }

    private static List<int> FindContentStarts(string sourceText, int regionStart, int regionEnd, string sep)
    {
        var contentStarts = new List<int> { regionStart };
        var searchFrom = regionStart;
        while (searchFrom <= regionEnd - sep.Length)
        {
            var pos = sourceText.IndexOf(sep, searchFrom, regionEnd - searchFrom, StringComparison.Ordinal);
            if (pos < 0) break;
            contentStarts.Add(pos + sep.Length);
            searchFrom = pos + sep.Length;
        }
        return contentStarts;
    }

    private static int PieceEnd(List<int> contentStarts, int pieceIndex, int regionEnd) =>
        pieceIndex + 1 < contentStarts.Count ? contentStarts[pieceIndex + 1] : regionEnd;

    private static int AdvanceScalars(string text, int offset, int maxScalars)
    {
        var start = offset;
        for (var i = 0; i < maxScalars && offset < text.Length; i++)
        {
            offset += char.IsHighSurrogate(text[offset])
                && offset + 1 < text.Length
                && char.IsLowSurrogate(text[offset + 1]) ? 2 : 1;
        }
        return offset - start;
    }

    private static int RetreatScalars(string text, int regionStart, int regionEnd, int maxScalars)
    {
        var pos = regionEnd;
        for (var i = 0; i < maxScalars && pos > regionStart; i++)
        {
            pos--;
            if (pos > regionStart && char.IsLowSurrogate(text[pos]) && char.IsHighSurrogate(text[pos - 1]))
                pos--;
        }
        return pos;
    }

    private static int AdvancePastCodePoint(string text, int pos)
    {
        if (pos >= text.Length) return text.Length;
        return pos + (char.IsHighSurrogate(text[pos])
            && pos + 1 < text.Length
            && char.IsLowSurrogate(text[pos + 1]) ? 2 : 1);
    }

    private static int AdjustSurrogates(string text, int cutEnd, int regionEnd)
    {
        if (cutEnd > 0 && cutEnd < regionEnd
            && char.IsHighSurrogate(text[cutEnd - 1])
            && char.IsLowSurrogate(text[cutEnd]))
        {
            cutEnd++;
        }
        return Math.Min(cutEnd, regionEnd);
    }

    private static int AdjustSurrogateStart(string text, int start, int lowerBound)
    {
        if (start > lowerBound && start < text.Length
            && char.IsLowSurrogate(text[start])
            && start > 0 && char.IsHighSurrogate(text[start - 1]))
        {
            start--;
        }
        return Math.Max(start, lowerBound);
    }
}
