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
    string? ErrorMessage = null)
{
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
