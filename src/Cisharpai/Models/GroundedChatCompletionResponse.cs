namespace Cisharpai.Models;

/// <summary>
/// Response from a grounded chat completion, extending the base response with citations.
/// </summary>
public sealed record GroundedChatCompletionResponse(
    /// <summary>
    /// The base chat completion response (content, tokens, raw data, etc.).
    /// </summary>
    ChatCompletionResponse ChatCompletion,

    /// <summary>
    /// Citations extracted from the response. Empty list if no citations were generated.
    /// </summary>
    IReadOnlyList<Citation> Citations,

    /// <summary>
    /// Indicates whether citations were produced natively by the provider or synthesized
    /// via prompt injection. Defaults to <see cref="GroundingKind.Native"/> for backward
    /// compatibility with providers that have native citation support.
    /// </summary>
    GroundingKind GroundingKind = GroundingKind.Native)
{
    /// <summary>
    /// Convenience: delegates to <see cref="ChatCompletionResponse.IsSuccess"/>.
    /// </summary>
    public bool IsSuccess => ChatCompletion.IsSuccess;

    /// <summary>
    /// Convenience: delegates to <see cref="ChatCompletionResponse.ErrorMessage"/>.
    /// </summary>
    public string? ErrorMessage => ChatCompletion.ErrorMessage;

    /// <summary>
    /// Convenience: delegates to <see cref="ChatCompletionResponse.Content"/>.
    /// </summary>
    public string Content => ChatCompletion.Content;

    public static GroundedChatCompletionResponse Error(string errorMessage, string? rawResponseJson = null) =>
        new(
            ChatCompletion: ChatCompletionResponse.Error(errorMessage, rawResponseJson),
            Citations: []);
}
