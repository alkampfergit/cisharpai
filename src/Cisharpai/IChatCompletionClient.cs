using Cisharpai.Features;
using Cisharpai.Models;

namespace Cisharpai;

public interface IChatCompletionClient : IHasFeatures
{
    Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
