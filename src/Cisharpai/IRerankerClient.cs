using Cisharpai.Features;
using Cisharpai.Models;

namespace Cisharpai;

public interface IRerankerClient : IHasFeatures
{
    Task<RerankResponse> RerankAsync(
        RerankRequest request,
        CancellationToken cancellationToken = default);
}
