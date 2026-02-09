using System.Text.Json;

namespace Cisharpai.Models;

public sealed record ChatCompletionRequest(
    IReadOnlyList<LlmMessage> Messages,
    string? Model = null,
    double? Temperature = null,
    int? MaxTokens = null,
    bool IncludeRawResponse = false,
    JsonElement? ExtraParameters = null);
