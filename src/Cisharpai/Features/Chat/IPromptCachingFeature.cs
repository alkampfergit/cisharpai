using Cisharpai.Models;

namespace Cisharpai.Features.Chat;

/// <summary>
/// Optional feature for prompt caching control.
/// Registered only by providers that expose explicit cache breakpoints (Anthropic).
/// Providers with automatic caching (OpenAI, Azure OpenAI) report cache usage on the
/// unified response without requiring this feature.
/// </summary>
public interface IPromptCachingFeature
{
    Task<ChatCompletionResponse> GetChatCompletionWithCachingAsync(
        ChatCompletionRequest request,
        PromptCachingOptions cachingOptions,
        CancellationToken cancellationToken = default);
}
