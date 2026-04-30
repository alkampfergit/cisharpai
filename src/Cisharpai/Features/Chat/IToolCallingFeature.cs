using Cisharpai.Models;

namespace Cisharpai.Features.Chat;

/// <summary>
/// Optional feature for tool calling (function calling) in chat completions.
/// Clients that support tool calling register this feature in their <see cref="IFeatureCollection"/>.
/// </summary>
public interface IToolCallingFeature
{
    /// <summary>
    /// Performs a chat completion with tool definitions, allowing the model to request tool invocations.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="toolOptions">Options controlling which tools are available and selection strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tool calling response containing the completion and any tool calls requested by the model.</returns>
    Task<ToolCallingResponse> GetChatCompletionWithToolsAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        CancellationToken cancellationToken = default);
}
