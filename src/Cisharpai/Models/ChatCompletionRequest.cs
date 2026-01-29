namespace Cisharpai.Models;

public sealed record ChatCompletionRequest(
    IReadOnlyList<LlmMessage> Messages,
    string Model,
    double? Temperature = null,
    int? MaxTokens = null,
    bool IncludeRawResponse = false);
