using Cisharpai.Models;

namespace Cisharpai;

public interface IChatCompletionClient
{
    Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
