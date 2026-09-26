using System.Globalization;
using Cisharpai.Rag.Models;
using Cisharpai.Rag.Packing;

namespace Cisharpai.Rag;

/// <summary>
/// Shared post-filter and validation helpers for <see cref="IRetriever"/> implementations.
/// </summary>
public static class RetrievalFiltering
{
    public static bool MatchesMetadata(TextChunk chunk, IReadOnlyDictionary<string, string> metadataEquals)
    {
        foreach (var (key, expectedValue) in metadataEquals)
        {
            if (!chunk.Metadata.TryGetValue(key, out var value))
                return false;

            var actual = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (!string.Equals(actual, expectedValue, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public static IReadOnlyList<ScoredChunk> ApplyPostFilters(
        IEnumerable<ScoredChunk> scored,
        RetrievalOptions options,
        int defaultTopK)
    {
        IEnumerable<ScoredChunk> filtered = scored;

        if (options.MetadataEquals is { Count: > 0 } metadataEquals)
        {
            filtered = filtered.Where(c => MatchesMetadata(c.Chunk, metadataEquals));
        }

        if (options.MinScore is not null)
        {
            filtered = filtered.Where(c => c.Score >= options.MinScore.Value);
        }

        var effectiveTopK = options.TopK ?? defaultTopK;

        return filtered
            .OrderByDescending(c => c.Score)
            .Take(effectiveTopK)
            .ToList();
    }

    public static void ThrowIfUnsupportedProviderQuery(
        RetrievalOptions options,
        string retrieverTypeName)
    {
        if (options.ProviderQuery is not null)
            throw new ArgumentException(
                $"{retrieverTypeName} does not support provider query extensions. " +
                $"Received provider query type '{options.ProviderQuery.GetType().FullName}'.",
                nameof(options));
    }
}
