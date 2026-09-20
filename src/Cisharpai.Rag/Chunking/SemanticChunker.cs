using System.Runtime.CompilerServices;
using Cisharpai.Rag.Embeddings;
using Cisharpai.Rag.Models;

namespace Cisharpai.Rag.Chunking;

/// <summary>
/// Places chunk boundaries where the meaning shifts by measuring cosine similarity between
/// consecutive sentence embeddings. <b>This chunker embeds the entire document at chunking time</b> —
/// it costs money and latency on top of any downstream embedding. A user swapping
/// <see cref="FixedSizeChunker"/> for this one should expect an additional embedding call
/// covering every sentence in the document.
/// <para>
/// Both <see cref="SemanticThresholdStrategy.Percentile"/> and
/// <see cref="SemanticThresholdStrategy.Absolute"/> modes buffer all sentence embeddings in
/// memory before the first chunk is emitted. Memory usage is proportional to
/// <c>sentences × embedding dimensions × 4 bytes</c>.
/// </para>
/// <para>
/// <b>Coverage:</b> emitted chunks cover the document exactly — no gaps, no overlap. Separator
/// text between sentences is attached to the preceding chunk, the first chunk is anchored at
/// offset 0, and the last chunk extends to the end of the document, so leading and trailing text
/// the sentence splitter did not claim is preserved rather than dropped. A document the splitter
/// finds no sentences in (for example, whitespace only) yields no chunks at all.
/// </para>
/// <para>
/// If the chunk for a single sentence — including the surrounding separator text it carries —
/// exceeds <see cref="SemanticChunkerOptions.MaxChunkCharacters"/>, an
/// <see cref="InvalidOperationException"/> is thrown, rather than emitting an over-budget chunk.
/// Use a larger limit or a sentence splitter that produces shorter segments.
/// </para>
/// </summary>
public sealed class SemanticChunker : ITextChunker
{
    private const float MinimumSimilaritySpread = 1e-6f;

    private readonly IBulkEmbeddingProcessor _embeddingProcessor;
    private readonly ISentenceSplitter _sentenceSplitter;
    private readonly SemanticChunkerOptions _options;

    /// <param name="embeddingProcessor">
    /// The processor to embed sentences. Must be configured with
    /// <see cref="EmbeddingInputType.Document"/> — sentence embeddings are corpus text,
    /// not queries. Using a query-configured processor silently degrades similarity quality
    /// on providers that honour the input-type hint (Cohere in particular).
    /// </param>
    /// <param name="options">Chunker options. Snapshotted at construction time.</param>
    /// <param name="sentenceSplitter">Custom sentence splitter; defaults to <see cref="RegexSentenceSplitter"/>.</param>
    public SemanticChunker(
        IBulkEmbeddingProcessor embeddingProcessor,
        SemanticChunkerOptions? options = null,
        ISentenceSplitter? sentenceSplitter = null)
    {
        ArgumentNullException.ThrowIfNull(embeddingProcessor);
        _embeddingProcessor = embeddingProcessor;
        _sentenceSplitter = sentenceSplitter ?? new RegexSentenceSplitter();
        _options = (options ?? new SemanticChunkerOptions()).Snapshot();
    }

    public IAsyncEnumerable<TextChunk> ChunkAsync(
        RagDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Id);
        ArgumentNullException.ThrowIfNull(document.Text);
        return _options.Strategy == SemanticThresholdStrategy.Percentile
            ? ChunkPercentileAsync(document, cancellationToken)
            : ChunkAbsoluteAsync(document, cancellationToken);
    }

    private async IAsyncEnumerable<TextChunk> ChunkPercentileAsync(
        RagDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sentences = SplitWithOffsets(document.Text);
        if (sentences.Count == 0)
            yield break;

        ValidateAllSentenceSizes(sentences, document.Text.Length);

        if (sentences.Count == 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return MakeChunk(document.Id, 0, document.Text, sentences, 0, 1);
            yield break;
        }

        var embeddings = await EmbedSentencesAsync(sentences, document.Id, cancellationToken)
            .ConfigureAwait(false);

        var similarities = ComputeConsecutiveSimilarities(embeddings);
        var threshold = ComputePercentileThreshold(similarities, _options.BreakPercentile);
        var boundaries = FindBoundaries(sentences, similarities, threshold, document.Text.Length);

        var chunkIndex = 0;
        var start = 0;
        foreach (var boundary in boundaries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return MakeChunk(document.Id, chunkIndex++, document.Text, sentences, start, boundary);
            start = boundary;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (start < sentences.Count)
            yield return MakeChunk(document.Id, chunkIndex, document.Text, sentences, start, sentences.Count);
    }

    private async IAsyncEnumerable<TextChunk> ChunkAbsoluteAsync(
        RagDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sentences = SplitWithOffsets(document.Text);
        if (sentences.Count == 0)
            yield break;

        ValidateAllSentenceSizes(sentences, document.Text.Length);

        if (sentences.Count == 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return MakeChunk(document.Id, 0, document.Text, sentences, 0, 1);
            yield break;
        }

        var embeddings = await EmbedSentencesAsync(sentences, document.Id, cancellationToken)
            .ConfigureAwait(false);

        var chunkIndex = 0;
        var chunkStart = 0;
        var chunkSentenceCount = 1;

        for (var i = 1; i < sentences.Count; i++)
        {
            var sim = VectorMath.CosineSimilarity(embeddings[i - 1], embeddings[i]);
            var candidateSpan = EmittedSpan(sentences, chunkStart, i + 1, document.Text.Length);
            var wouldExceedChars = candidateSpan > _options.MaxChunkCharacters;
            var wouldExceedSentences = chunkSentenceCount + 1 > _options.MaxChunkSentences;
            var belowThreshold = sim < _options.AbsoluteThreshold;

            if (belowThreshold || wouldExceedChars || wouldExceedSentences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return MakeChunk(document.Id, chunkIndex++, document.Text, sentences, chunkStart, i);
                chunkStart = i;
                chunkSentenceCount = 1;
            }
            else
            {
                chunkSentenceCount++;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (chunkStart < sentences.Count)
            yield return MakeChunk(document.Id, chunkIndex, document.Text, sentences, chunkStart, sentences.Count);
    }

    private List<(string Text, int StartOffset, int EndOffset)> SplitWithOffsets(string text)
    {
        var rawSentences = _sentenceSplitter.Split(text);
        var result = new List<(string, int, int)>(rawSentences.Count);
        var searchFrom = 0;

        foreach (var sentence in rawSentences)
        {
            var idx = text.IndexOf(sentence, searchFrom, StringComparison.Ordinal);
            if (idx < 0)
                idx = text.IndexOf(sentence, StringComparison.Ordinal);

            if (idx >= 0)
            {
                result.Add((sentence, idx, idx + sentence.Length));
                searchFrom = idx + sentence.Length;
            }
        }
        return result;
    }

    private async Task<float[][]> EmbedSentencesAsync(
        List<(string Text, int StartOffset, int EndOffset)> sentences,
        string documentId,
        CancellationToken cancellationToken)
    {
        var sentenceChunks = sentences
            .Select((s, i) => new TextChunk(documentId, i, s.StartOffset, s.EndOffset, s.Text))
            .ToList();

        var embeddings = new float[sentences.Count][];
        await foreach (var batch in _embeddingProcessor
            .EmbedAsync(sentenceChunks, cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!batch.IsSuccess)
                throw new InvalidOperationException(
                    $"Sentence embedding batch {batch.BatchIndex} failed: {batch.ErrorMessage}");

            foreach (var item in batch.Items)
                embeddings[item.Chunk.Index] = item.Vector;
        }

        return embeddings;
    }

    private static float[] ComputeConsecutiveSimilarities(float[][] embeddings)
    {
        var similarities = new float[embeddings.Length - 1];
        for (var i = 0; i < similarities.Length; i++)
            similarities[i] = VectorMath.CosineSimilarity(embeddings[i], embeddings[i + 1]);
        return similarities;
    }

    private static float ComputePercentileThreshold(float[] similarities, float breakPercentile)
    {
        if (similarities.Length == 0)
            return 0f;

        var sorted = (float[])similarities.Clone();
        Array.Sort(sorted);

        if (sorted[^1] - sorted[0] <= MinimumSimilaritySpread)
            return float.NegativeInfinity;

        var rank = breakPercentile / 100f * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        var weight = rank - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }

    private List<int> FindBoundaries(
        List<(string Text, int StartOffset, int EndOffset)> sentences,
        float[] similarities,
        float threshold,
        int textLength)
    {
        var boundaries = new List<int>();
        var chunkStartIdx = 0;
        var chunkSentenceCount = 1;

        for (var i = 0; i < similarities.Length; i++)
        {
            var candidateSpan = EmittedSpan(sentences, chunkStartIdx, i + 2, textLength);
            var wouldExceedChars = candidateSpan > _options.MaxChunkCharacters;
            var wouldExceedSentences = chunkSentenceCount + 1 > _options.MaxChunkSentences;
            var belowThreshold = similarities[i] <= threshold;

            if (belowThreshold || wouldExceedChars || wouldExceedSentences)
            {
                boundaries.Add(i + 1);
                chunkStartIdx = i + 1;
                chunkSentenceCount = 1;
            }
            else
            {
                chunkSentenceCount++;
            }
        }
        return boundaries;
    }

    private void ValidateAllSentenceSizes(
        List<(string Text, int StartOffset, int EndOffset)> sentences,
        int textLength)
    {
        for (var i = 0; i < sentences.Count; i++)
        {
            // Measure what a single-sentence chunk would actually emit, not the sentence alone:
            // the emitted span also carries the separator text up to the next sentence, plus any
            // leading text for the first sentence and trailing text for the last.
            var span = EmittedSpan(sentences, i, i + 1, textLength);
            if (span > _options.MaxChunkCharacters)
                throw new InvalidOperationException(
                    $"The chunk for the sentence at offset {sentences[i].StartOffset} spans {span} characters " +
                    $"(including surrounding separator text), which exceeds MaxChunkCharacters " +
                    $"({_options.MaxChunkCharacters}). " +
                    "Use a larger MaxChunkCharacters or a sentence splitter that produces shorter segments.");
        }
    }

    /// <summary>
    /// Start offset of the chunk beginning at <paramref name="fromInclusive"/>. The first chunk is
    /// anchored at 0 so text the sentence splitter did not claim is never dropped.
    /// </summary>
    private static int EmittedStart(
        List<(string Text, int StartOffset, int EndOffset)> sentences,
        int fromInclusive) =>
        fromInclusive == 0 ? 0 : sentences[fromInclusive].StartOffset;

    /// <summary>
    /// Exclusive end offset of the chunk ending before <paramref name="toExclusive"/>. Non-final
    /// chunks extend to the next sentence start so inter-sentence separators stay attached to the
    /// preceding chunk; the final chunk extends to the end of the document so trailing text
    /// (whitespace, or a fragment the splitter rejected) is never dropped.
    /// </summary>
    private static int EmittedEnd(
        List<(string Text, int StartOffset, int EndOffset)> sentences,
        int toExclusive,
        int textLength) =>
        toExclusive < sentences.Count ? sentences[toExclusive].StartOffset : textLength;

    private static int EmittedSpan(
        List<(string Text, int StartOffset, int EndOffset)> sentences,
        int chunkStart,
        int toExclusive,
        int textLength) =>
        EmittedEnd(sentences, toExclusive, textLength) - EmittedStart(sentences, chunkStart);

    private static TextChunk MakeChunk(
        string documentId,
        int chunkIndex,
        string documentText,
        List<(string Text, int StartOffset, int EndOffset)> sentences,
        int fromInclusive,
        int toExclusive)
    {
        var startOffset = EmittedStart(sentences, fromInclusive);
        var endOffset = EmittedEnd(sentences, toExclusive, documentText.Length);
        var text = documentText[startOffset..endOffset];
        return new TextChunk(documentId, chunkIndex, startOffset, endOffset, text);
    }
}
