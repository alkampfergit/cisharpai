using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Features.Embeddings;

public interface IMultimodalEmbeddingFeature
{
    Task<EmbeddingResponse> GetMultimodalEmbeddingsAsync(
        IReadOnlyList<MultimodalEmbeddingInput> inputs,
        string model,
        EmbeddingInputType? inputType = null,
        int? outputDimension = null,
        bool includeRawResponse = false,
        JsonElement? extraParameters = null,
        CancellationToken cancellationToken = default);
}
