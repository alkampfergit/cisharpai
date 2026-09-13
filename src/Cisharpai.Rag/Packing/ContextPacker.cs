namespace Cisharpai.Rag.Packing;

/// <summary>
/// Greedy context packer: selects chunks in rank order within a token budget,
/// optionally reorders the selection using the lost-in-the-middle heuristic.
/// </summary>
public sealed class ContextPacker : IContextPacker
{
    private readonly ITokenCounter _counter;

    public ContextPacker(ITokenCounter counter)
    {
        ArgumentNullException.ThrowIfNull(counter);
        _counter = counter;
    }

    public async Task<ContextPackingResult> PackAsync(
        IReadOnlyList<ScoredChunk> rankedChunks,
        ContextPackingOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rankedChunks);
        ArgumentNullException.ThrowIfNull(options);
        var opts = options.Snapshot();

        var effectiveBudget = opts.TokenBudget - opts.ReservedTokens;
        var separatorTokens = opts.Separator.Length > 0
            ? await _counter.CountAsync(opts.Separator, cancellationToken).ConfigureAwait(false)
            : 0;

        // Count each chunk once
        var chunkTokenCounts = new int[rankedChunks.Count];
        for (var i = 0; i < rankedChunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            chunkTokenCounts[i] = await _counter.CountAsync(
                rankedChunks[i].Chunk.Text, cancellationToken).ConfigureAwait(false);
        }

        var selected = new List<(ScoredChunk Chunk, int TokenCount)>();
        var dropped = new List<DroppedChunk>();
        var tokensUsed = 0;

        for (var i = 0; i < rankedChunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunkTokens = chunkTokenCounts[i];
            var separatorCost = selected.Count > 0 ? separatorTokens : 0;
            var totalCost = chunkTokens + separatorCost;

            if (chunkTokens > effectiveBudget)
            {
                dropped.Add(new DroppedChunk(rankedChunks[i], chunkTokens, DropReason.IndividuallyOversized));
                continue;
            }

            if (tokensUsed + totalCost > effectiveBudget)
            {
                if (opts.OverflowStrategy == OverflowStrategy.StopAtFirstMisfit)
                {
                    dropped.Add(new DroppedChunk(rankedChunks[i], chunkTokens, DropReason.BudgetExhausted));
                    for (var j = i + 1; j < rankedChunks.Count; j++)
                        dropped.Add(new DroppedChunk(rankedChunks[j], chunkTokenCounts[j], DropReason.BudgetExhausted));
                    break;
                }

                dropped.Add(new DroppedChunk(rankedChunks[i], chunkTokens, DropReason.BudgetExhausted));
                continue;
            }

            tokensUsed += totalCost;
            selected.Add((rankedChunks[i], chunkTokens));
        }

        IReadOnlyList<ScoredChunk> ordered = opts.UseLostInMiddleOrdering && selected.Count > 1
            ? ApplyLostInMiddleOrdering(selected.Select(s => s.Chunk).ToList())
            : selected.Select(s => s.Chunk).ToList();

        return new ContextPackingResult(
            ordered,
            dropped,
            tokensUsed,
            opts.TokenBudget - opts.ReservedTokens - tokensUsed);
    }

    /// <summary>
    /// Places the highest-ranked chunks at the edges (start and end) and the
    /// weakest in the middle, because models attend most reliably to context edges.
    /// Input order is rank order (index 0 = highest rank).
    /// </summary>
    internal static IReadOnlyList<ScoredChunk> ApplyLostInMiddleOrdering(
        IReadOnlyList<ScoredChunk> rankOrdered)
    {
        if (rankOrdered.Count <= 2)
            return rankOrdered;

        var result = new ScoredChunk[rankOrdered.Count];
        var left = 0;
        var right = rankOrdered.Count - 1;

        for (var i = 0; i < rankOrdered.Count; i++)
        {
            if (i % 2 == 0)
                result[left++] = rankOrdered[i];
            else
                result[right--] = rankOrdered[i];
        }

        return result;
    }
}
