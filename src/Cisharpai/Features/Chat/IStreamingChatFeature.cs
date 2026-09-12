using Cisharpai.Models;

namespace Cisharpai.Features.Chat;

/// <summary>
/// Optional feature for streaming chat completions via server-sent events (SSE).
/// Discovered via <c>client.Features.Get&lt;IStreamingChatFeature&gt;()</c>.
/// </summary>
public interface IStreamingChatFeature
{
    /// <summary>
    /// Streams chat completion chunks as they arrive from the provider.
    /// </summary>
    IAsyncEnumerable<ChatCompletionChunk> GetChatCompletionStreamAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
