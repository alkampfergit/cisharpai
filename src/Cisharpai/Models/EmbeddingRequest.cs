using System.Text.Json;

namespace Cisharpai.Models;

/// <summary>
/// Unified request model for embedding operations across all providers.
/// </summary>
public sealed record EmbeddingRequest(
    IReadOnlyList<string> Input,
    string? Model = null,
    EmbeddingInputType? InputType = null,
    int? Dimensions = null,
    string? EncodingFormat = null,
    bool IncludeRawResponse = false,
    JsonElement? ExtraParameters = null);
