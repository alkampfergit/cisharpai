namespace cisharpai.Models;

public sealed record ChatCompletionResponse(
    string Content,
    string Model,
    int PromptTokens,
    int CompletionTokens);
