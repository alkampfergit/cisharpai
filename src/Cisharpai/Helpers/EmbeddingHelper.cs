using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Helpers;

/// <summary>
/// Shared helper for mapping embedding responses across providers with identical response formats.
/// </summary>
public static class EmbeddingHelper
{
    /// <summary>
    /// Maps ordered embedding data items to a unified EmbeddingResponse.
    /// Each data item provides an Embedding (JsonElement) that may be a float array or base64 string.
    /// </summary>
    public static EmbeddingResponse MapEmbeddingResponse(
        IReadOnlyList<JsonElement> orderedEmbeddings,
        string? encodingFormat,
        string model,
        int totalTokens,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        var isBase64 = string.Equals(encodingFormat, "base64", StringComparison.OrdinalIgnoreCase);

        List<float[]> embeddings;
        IReadOnlyList<string>? base64Embeddings = null;

        if (isBase64)
        {
            embeddings = [];
            base64Embeddings = orderedEmbeddings
                .Select(e => e.GetString() ?? string.Empty)
                .ToList();
        }
        else
        {
            embeddings = orderedEmbeddings
                .Select(e => e.EnumerateArray().Select(v => v.GetSingle()).ToArray())
                .ToList();
        }

        var dimensions = !isBase64 && embeddings.Count > 0
            ? embeddings[0].Length
            : (int?)null;

        return new EmbeddingResponse(
            Embeddings: embeddings,
            Base64Embeddings: base64Embeddings,
            Model: model,
            TotalTokens: totalTokens,
            Dimensions: dimensions,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);
    }
}
