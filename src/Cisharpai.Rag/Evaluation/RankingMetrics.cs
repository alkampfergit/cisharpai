namespace Cisharpai.Rag.Evaluation;

/// <summary>
/// Pure-function ranking metrics for evaluating retrieval quality.
/// </summary>
public static class RankingMetrics
{
    /// <summary>
    /// Normalized Discounted Cumulative Gain: measures ranking quality by comparing
    /// the actual ranking to the ideal ranking, penalizing relevant items placed lower.
    /// Returns 0 when no relevant items exist or the retrieved list is empty.
    /// </summary>
    public static double Ndcg(IReadOnlyList<string> retrievedIds, IReadOnlySet<string> relevantIds)
    {
        ArgumentNullException.ThrowIfNull(retrievedIds);
        ArgumentNullException.ThrowIfNull(relevantIds);

        if (retrievedIds.Count == 0 || relevantIds.Count == 0)
            return 0.0;

        var seen = new HashSet<string>();
        var dcg = 0.0;
        for (var i = 0; i < retrievedIds.Count; i++)
        {
            if (relevantIds.Contains(retrievedIds[i]) && seen.Add(retrievedIds[i]))
                dcg += 1.0 / Math.Log2(i + 2);
        }

        var idealCount = Math.Min(relevantIds.Count, retrievedIds.Count);
        var idcg = 0.0;
        for (var i = 0; i < idealCount; i++)
            idcg += 1.0 / Math.Log2(i + 2);

        return idcg == 0.0 ? 0.0 : dcg / idcg;
    }

    /// <inheritdoc cref="Ndcg(IReadOnlyList{string}, IReadOnlySet{string})"/>
    public static double Ndcg(IReadOnlyList<string> retrievedIds, IReadOnlyList<string> relevantIds)
    {
        ArgumentNullException.ThrowIfNull(relevantIds);
        return Ndcg(retrievedIds, new HashSet<string>(relevantIds));
    }

    /// <summary>
    /// Mean Reciprocal Rank: the reciprocal of the rank of the first relevant item.
    /// Returns 0 when no relevant item is found.
    /// </summary>
    public static double Mrr(IReadOnlyList<string> retrievedIds, IReadOnlySet<string> relevantIds)
    {
        ArgumentNullException.ThrowIfNull(retrievedIds);
        ArgumentNullException.ThrowIfNull(relevantIds);

        for (var i = 0; i < retrievedIds.Count; i++)
        {
            if (relevantIds.Contains(retrievedIds[i]))
                return 1.0 / (i + 1);
        }

        return 0.0;
    }

    /// <inheritdoc cref="Mrr(IReadOnlyList{string}, IReadOnlySet{string})"/>
    public static double Mrr(IReadOnlyList<string> retrievedIds, IReadOnlyList<string> relevantIds)
    {
        ArgumentNullException.ThrowIfNull(relevantIds);
        return Mrr(retrievedIds, new HashSet<string>(relevantIds));
    }

    /// <summary>
    /// Recall at k: the fraction of relevant items that appear in the top k retrieved results.
    /// Returns 0 when there are no relevant items.
    /// </summary>
    public static double RecallAtK(IReadOnlyList<string> retrievedIds, IReadOnlySet<string> relevantIds, int k)
    {
        ArgumentNullException.ThrowIfNull(retrievedIds);
        ArgumentNullException.ThrowIfNull(relevantIds);
        if (k <= 0)
            throw new ArgumentOutOfRangeException(nameof(k), k, "k must be positive.");

        if (relevantIds.Count == 0)
            return 0.0;

        var limit = Math.Min(k, retrievedIds.Count);
        var seen = new HashSet<string>();
        var found = 0;
        for (var i = 0; i < limit; i++)
        {
            if (relevantIds.Contains(retrievedIds[i]) && seen.Add(retrievedIds[i]))
                found++;
        }

        return (double)found / relevantIds.Count;
    }

    /// <inheritdoc cref="RecallAtK(IReadOnlyList{string}, IReadOnlySet{string}, int)"/>
    public static double RecallAtK(IReadOnlyList<string> retrievedIds, IReadOnlyList<string> relevantIds, int k)
    {
        ArgumentNullException.ThrowIfNull(relevantIds);
        return RecallAtK(retrievedIds, new HashSet<string>(relevantIds), k);
    }
}
