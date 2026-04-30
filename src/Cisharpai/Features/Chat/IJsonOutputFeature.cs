using Cisharpai.Models;

namespace Cisharpai.Features.Chat;

/// <summary>
/// Optional feature for enforcing JSON output in chat completions.
/// Supports both JSON Mode (json_object) and Structured Outputs (json_schema).
/// Clients that support JSON output register this feature in their <see cref="IFeatureCollection"/>.
/// </summary>
public interface IJsonOutputFeature
{
    /// <summary>
    /// Performs a chat completion with JSON output enforcement.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="jsonOutputOptions">Options controlling JSON output mode and schema.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chat completion response. Check <see cref="ChatCompletionResponse.Refusal"/>
    /// for Structured Outputs safety refusals.</returns>
    Task<ChatCompletionResponse> GetChatCompletionWithJsonOutputAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOutputOptions,
        CancellationToken cancellationToken = default);
}
