using Cisharpai.Models;

namespace Cisharpai.Features.Chat;

/// <summary>
/// Optional feature for provider-hosted web search as a retrieval source.
/// The model decides whether to search; the search executes server-side within a single
/// request — no round trip, no search API key required from the caller.
/// <para>
/// <strong>Cost warning:</strong> each search incurs a per-search charge on top of token
/// costs (e.g. ~$10 per 1,000 searches on Anthropic). Monitor
/// <see cref="ChatCompletionResponse.WebSearchCount"/> to track billable searches.
/// </para>
/// </summary>
public interface IWebSearchFeature
{
    /// <summary>
    /// Performs a chat completion with server-side web search enabled.
    /// The model may invoke the search tool zero or more times; results are cited in the response.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="webSearchOptions">Options controlling the web search tool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A grounded chat response containing the completion and web-search citations.</returns>
    Task<GroundedChatCompletionResponse> GetChatCompletionWithWebSearchAsync(
        ChatCompletionRequest request,
        WebSearchOptions webSearchOptions,
        CancellationToken cancellationToken = default);
}
