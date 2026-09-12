namespace Cisharpai.Models;

/// <summary>
/// A single reranked document.
/// </summary>
/// <param name="Index">
/// Zero-based position of the document in the originating <see cref="RerankRequest.Documents"/> list.
/// </param>
/// <param name="RelevanceScore">
/// Provider-assigned relevance score. Score scales are provider-defined, so compare values
/// within a single response rather than across providers.
/// </param>
public sealed record RerankResult(
    int Index,
    double RelevanceScore);
