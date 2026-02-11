namespace Cisharpai.Models;

/// <summary>
/// Response from a chat completion with tool calling, extending the base response with tool calls.
/// </summary>
public sealed record ToolCallingResponse(
    /// <summary>
    /// The base chat completion response (content, tokens, raw data, etc.).
    /// </summary>
    ChatCompletionResponse ChatCompletion,

    /// <summary>
    /// Tool invocations requested by the model. Null or empty if the model chose to generate text instead.
    /// </summary>
    IReadOnlyList<ToolCall>? ToolCalls)
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

    /// <summary>
    /// Creates an error response.
    /// </summary>
    public static ToolCallingResponse Error(string errorMessage, string? rawResponseJson = null) =>
        new(
            ChatCompletion: ChatCompletionResponse.Error(errorMessage, rawResponseJson),
            ToolCalls: null);
}
