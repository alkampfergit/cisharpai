using Cisharpai.Models;

namespace Cisharpai;

public interface IEmbeddingClient
{
    Task<EmbeddingResponse> GetEmbeddingsAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default);
}
