using cisharpai.Models;

namespace cisharpai;

public interface IChatCompletionClient
{
    Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
