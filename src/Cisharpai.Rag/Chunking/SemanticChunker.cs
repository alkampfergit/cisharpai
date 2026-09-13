using System.Runtime.CompilerServices;
using Cisharpai.Models;
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
/// In <see cref="SemanticThresholdStrategy.Percentile"/> mode (the default), all sentence
/// embeddings are buffered in memory before the first chunk is emitted. Memory usage is
/// proportional to <c>sentences × embedding dimensions × 4 bytes</c>.
/// </para>
/// <para>
/// In <see cref="SemanticThresholdStrategy.Absolute"/> mode, chunks can be emitted as
/// embedding batches return — true streaming.
/// </para>
/// </summary>
public sealed class SemanticChunker : ITextChunker
{
    private readonly IBulkEmbeddingProcessor _embeddingProcessor;
    private readonly ISentenceSplitter _sentenceSplitter;
    private readonly SemanticChunkerOptions _options;

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
        if (sentences.Count == 1)
        {
            yield return MakeChunk(document.Id, 0, sentences, 0, 1);
            yield break;
        }

        var embeddings = await EmbedSentencesAsync(sentences, document.Id, cancellationToken)
            .ConfigureAwait(false);

        var similarities = ComputeConsecutiveSimilarities(embeddings);
        var threshold = ComputePercentileThreshold(similarities, _options.BreakPercentile);
        var boundaries = FindBoundaries(sentences, similarities, threshold);

        var chunkIndex = 0;
        var start = 0;
        foreach (var boundary in boundaries)
        {
            yield return MakeChunk(document.Id, chunkIndex++, sentences, start, boundary);
            start = boundary;
        }
        if (start < sentences.Count)
            yield return MakeChunk(document.Id, chunkIndex, sentences, start, sentences.Count);
    }

    private async IAsyncEnumerable<TextChunk> ChunkAbsoluteAsync(
        RagDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sentences = SplitWithOffsets(document.Text);
        if (sentences.Count == 0)
            yield break;
        if (sentences.Count == 1)
        {
            yield return MakeChunk(document.Id, 0, sentences, 0, 1);
            yield break;
        }

        var embeddings = await EmbedSentencesAsync(sentences, document.Id, cancellationToken)
            .ConfigureAwait(false);

        var chunkIndex = 0;
        var chunkStart = 0;
        var chunkCharCount = sentences[0].Text.Length;
        var chunkSentenceCount = 1;

        for (var i = 1; i < sentences.Count; i++)
        {
            var sim = VectorMath.CosineSimilarity(embeddings[i - 1], embeddings[i]);
            var sentLen = sentences[i].Text.Length;
            var wouldExceedChars = chunkCharCount + sentLen > _options.MaxChunkCharacters;
            var wouldExceedSentences = chunkSentenceCount + 1 > _options.MaxChunkSentences;
            var belowThreshold = sim < _options.AbsoluteThreshold;

            if (belowThreshold || wouldExceedChars || wouldExceedSentences)
            {
                yield return MakeChunk(document.Id, chunkIndex++, sentences, chunkStart, i);
                chunkStart = i;
                chunkCharCount = sentLen;
                chunkSentenceCount = 1;
            }
            else
            {
                chunkCharCount += sentLen;
                chunkSentenceCount++;
            }
        }

        if (chunkStart < sentences.Count)
            yield return MakeChunk(document.Id, chunkIndex, sentences, chunkStart, sentences.Count);
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
        var rank = breakPercentile / 100f * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        var weight = rank - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }

    private List<int> FindBoundaries(
        List<(string Text, int StartOffset, int EndOffset)> sentences,
        float[] similarities,
        float threshold)
    {
        var boundaries = new List<int>();
        var chunkCharCount = sentences[0].Text.Length;
        var chunkSentenceCount = 1;

        for (var i = 0; i < similarities.Length; i++)
        {
            var sentLen = sentences[i + 1].Text.Length;
            var wouldExceedChars = chunkCharCount + sentLen > _options.MaxChunkCharacters;
            var wouldExceedSentences = chunkSentenceCount + 1 > _options.MaxChunkSentences;
            var belowThreshold = similarities[i] <= threshold;

            if (belowThreshold || wouldExceedChars || wouldExceedSentences)
            {
                boundaries.Add(i + 1);
                chunkCharCount = sentLen;
                chunkSentenceCount = 1;
            }
            else
            {
                chunkCharCount += sentLen;
                chunkSentenceCount++;
            }
        }
        return boundaries;
    }

    private static TextChunk MakeChunk(
        string documentId,
        int chunkIndex,
        List<(string Text, int StartOffset, int EndOffset)> sentences,
        int fromInclusive,
        int toExclusive)
    {
        var startOffset = sentences[fromInclusive].StartOffset;
        var endOffset = sentences[toExclusive - 1].EndOffset;
        var text = string.Join(" ", sentences.Skip(fromInclusive).Take(toExclusive - fromInclusive).Select(s => s.Text));
        return new TextChunk(documentId, chunkIndex, startOffset, endOffset, text);
    }
}
