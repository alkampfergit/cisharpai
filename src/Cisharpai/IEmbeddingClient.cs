using Cisharpai.Features;
using Cisharpai.Models;

namespace Cisharpai;

public interface IEmbeddingClient : IHasFeatures
{
    Task<EmbeddingResponse> GetEmbeddingsAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default);
}
