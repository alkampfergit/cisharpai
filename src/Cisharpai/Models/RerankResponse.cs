namespace Cisharpai.Models;

/// <summary>
/// Unified response model for reranking operations across all providers.
/// </summary>
/// <param name="Results">Ranked results, most relevant first.</param>
/// <param name="Model">The model that produced the ranking.</param>
/// <param name="SearchUnits">Billed search units, when the provider reports them.</param>
/// <param name="InputTokens">Billed input tokens, when the provider reports them.</param>
public sealed record RerankResponse(
    IReadOnlyList<RerankResult> Results,
    string Model,
    int? SearchUnits = null,
    int? InputTokens = null,
    string? RawResponseJson = null,
    string? RawRequestJson = null,
    bool IsSuccess = true,
    string? ErrorMessage = null)
{
    public static RerankResponse Error(string errorMessage, string? rawResponseJson = null, string? rawRequestJson = null) =>
        new(
            Results: [],
            Model: string.Empty,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            IsSuccess: false,
            ErrorMessage: errorMessage);
}
