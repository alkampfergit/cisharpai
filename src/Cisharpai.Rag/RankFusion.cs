using Cisharpai.Rag.Packing;

namespace Cisharpai.Rag;

/// <summary>
/// Fuses multiple ranked result lists into a single ranked list.
/// </summary>
public static class RankFusion
{
    private const double DefaultK = 60.0;

    /// <summary>
    /// Reciprocal Rank Fusion (RRF): merges multiple ranked lists into one by summing
    /// <c>1 / (k + rank)</c> across all lists in which an item appears.
    /// Items present in only one list receive their single-list RRF score.
    /// </summary>
    /// <param name="rankedLists">One or more ranked result lists, each in descending relevance order.</param>
    /// <param name="k">Smoothing constant (default 60). Higher values reduce the influence of high-rank items.</param>
    /// <returns>Fused list sorted by descending RRF score.</returns>
    public static IReadOnlyList<ScoredChunk> ReciprocalRank(
        IReadOnlyList<IReadOnlyList<ScoredChunk>> rankedLists,
        double k = DefaultK)
    {
        ArgumentNullException.ThrowIfNull(rankedLists);
        if (!double.IsFinite(k) || k <= 0)
            throw new ArgumentOutOfRangeException(nameof(k), k, "k must be a finite positive number.");

        var scores = new Dictionary<TextChunkIdentity, (double Score, ScoredChunk Representative)>();

        foreach (var list in rankedLists)
        {
            if (list is null)
                continue;

            for (var rank = 0; rank < list.Count; rank++)
            {
                var item = list[rank];
                var rrfScore = 1.0 / (k + rank + 1);
                var key = new TextChunkIdentity(item.Chunk.DocumentId, item.Chunk.Index);

                if (scores.TryGetValue(key, out var existing))
                    scores[key] = (existing.Score + rrfScore, existing.Representative);
                else
                    scores[key] = (rrfScore, item);
            }
        }

        var result = new List<ScoredChunk>(scores.Count);
        foreach (var (_, (score, representative)) in scores)
            result.Add(new ScoredChunk(representative.Chunk, score));

        result.Sort((a, b) => b.Score.CompareTo(a.Score));
        return result;
    }

    private readonly record struct TextChunkIdentity(string DocumentId, int Index);
}
