namespace Cisharpai.Models;

public sealed record ChatCompletionResponse(
    string Content,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    string? RawResponseJson = null,
    string? RawRequestJson = null,
    string? Status = null,
    string? IncompleteReason = null,
    bool IsSuccess = true,
    string? ErrorMessage = null,
    /// <summary>
    /// When using Structured Outputs, the model may refuse to generate output for safety reasons.
    /// When non-null, Content may be empty and the caller should check this field.
    /// </summary>
    string? Refusal = null,
    int? CachedInputTokens = null,
    int? CacheCreationInputTokens = null)
{
    /// <summary>
    /// Number of web searches performed by a provider-hosted search tool during this request.
    /// <c>null</c> when the provider reported no web-search usage field at all (i.e. web search
    /// was not involved in the request). <c>0</c> means the provider explicitly reported zero
    /// searches (the search tool was present but the model chose not to search).
    /// Each search is billed separately from tokens.
    /// </summary>
    public int? WebSearchCount { get; init; }

    public static ChatCompletionResponse Error(string errorMessage, string? rawResponseJson = null) =>
        new(
            Content: string.Empty,
            Model: string.Empty,
            PromptTokens: 0,
            CompletionTokens: 0,
            RawResponseJson: rawResponseJson,
            IsSuccess: false,
            ErrorMessage: errorMessage);
}
