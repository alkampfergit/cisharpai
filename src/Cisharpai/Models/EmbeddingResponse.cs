namespace Cisharpai.Models;

/// <summary>
/// Unified response model for embedding operations across all providers.
/// </summary>
public sealed record EmbeddingResponse(
    IReadOnlyList<float[]> Embeddings,
    IReadOnlyList<string>? Base64Embeddings,
    string Model,
    int TotalTokens,
    int? Dimensions = null,
    string? RawResponseJson = null,
    string? RawRequestJson = null,
    bool IsSuccess = true,
    string? ErrorMessage = null)
{
    public static EmbeddingResponse Error(string errorMessage, string? rawResponseJson = null) =>
        new(
            Embeddings: [],
            Base64Embeddings: null,
            Model: string.Empty,
            TotalTokens: 0,
            RawResponseJson: rawResponseJson,
            IsSuccess: false,
            ErrorMessage: errorMessage);
}
