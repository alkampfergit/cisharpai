using Cisharpai.Models;

namespace Cisharpai.Features.Chat;

/// <summary>
/// Optional feature for grounded chat (RAG) with document citations.
/// Clients that support grounded chat register this feature in their <see cref="IFeatureCollection"/>.
/// </summary>
public interface IGroundedChatFeature
{
    /// <summary>
    /// Performs a chat completion grounded on provided documents, with citations.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="groundedChatOptions">Options controlling documents and citation mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A grounded chat response containing the completion and citations.</returns>
    Task<GroundedChatCompletionResponse> GetGroundedChatCompletionAsync(
        ChatCompletionRequest request,
        GroundedChatOptions groundedChatOptions,
        CancellationToken cancellationToken = default);
}
