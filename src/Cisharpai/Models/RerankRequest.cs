using System.Text.Json;

namespace Cisharpai.Models;

/// <summary>
/// Unified request model for reranking operations across all providers.
/// </summary>
/// <param name="Query">The query the documents are ranked against.</param>
/// <param name="Documents">
/// The candidate documents, in the caller's own order.
/// <see cref="RerankResult.Index"/> refers back into this list.
/// </param>
/// <param name="Model">
/// Provider model identifier. Falls back to the client's configured default model.
/// </param>
/// <param name="TopN">Return only the top N results. <c>null</c> returns all documents.</param>
/// <param name="MaxTokensPerDocument">Per-document truncation budget.</param>
public sealed record RerankRequest(
    string Query,
    IReadOnlyList<string> Documents,
    string? Model = null,
    int? TopN = null,
    int? MaxTokensPerDocument = null,
    bool IncludeRawResponse = false,
    JsonElement? ExtraParameters = null);
