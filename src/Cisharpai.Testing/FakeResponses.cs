using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Testing;

/// <summary>
/// Static factory methods for creating common fake responses.
/// </summary>
public static class FakeResponses
{
    private const string DefaultModel = "fake-model";
    /// <summary>
    /// Creates a successful chat completion response.
    /// </summary>
    public static ChatCompletionResponse Chat(
        string content,
        string model = DefaultModel,
        int promptTokens = 10,
        int completionTokens = 5) =>
        new(Content: content, Model: model, PromptTokens: promptTokens, CompletionTokens: completionTokens);

    /// <summary>
    /// Creates a chat completion error response.
    /// </summary>
    public static ChatCompletionResponse ChatError(string errorMessage) =>
        ChatCompletionResponse.Error(errorMessage);

    /// <summary>
    /// Creates a tool calling response with one or more tool calls.
    /// </summary>
    public static ToolCallingResponse ToolCall(
        string functionName,
        string argumentsJson,
        string? id = null,
        string model = DefaultModel) =>
        new(
            ChatCompletion: new ChatCompletionResponse(
                Content: string.Empty,
                Model: model,
                PromptTokens: 10,
                CompletionTokens: 5),
            ToolCalls: [new Models.ToolCall(
                Id: id ?? Guid.NewGuid().ToString(),
                FunctionName: functionName,
                Arguments: JsonDocument.Parse(argumentsJson).RootElement)]);

    /// <summary>
    /// Creates a tool calling response with multiple tool calls.
    /// </summary>
    public static ToolCallingResponse ToolCalls(
        params (string FunctionName, string ArgumentsJson)[] calls) =>
        new(
            ChatCompletion: new ChatCompletionResponse(
                Content: string.Empty,
                Model: DefaultModel,
                PromptTokens: 10,
                CompletionTokens: 5),
            ToolCalls: calls.Select(c => new Models.ToolCall(
                Id: Guid.NewGuid().ToString(),
                FunctionName: c.FunctionName,
                Arguments: JsonDocument.Parse(c.ArgumentsJson).RootElement)).ToList());

    /// <summary>
    /// Creates a grounded chat response with citations.
    /// </summary>
    public static GroundedChatCompletionResponse GroundedChat(
        string content,
        IReadOnlyList<Citation>? citations = null,
        string model = DefaultModel) =>
        new(
            ChatCompletion: new ChatCompletionResponse(
                Content: content,
                Model: model,
                PromptTokens: 10,
                CompletionTokens: 5),
            Citations: citations ?? []);

    /// <summary>
    /// Creates streaming chunks from text segments. The last chunk includes a "stop" finish reason.
    /// </summary>
    public static IReadOnlyList<ChatCompletionChunk> StreamingChunks(params string[] textSegments)
    {
        var chunks = new List<ChatCompletionChunk>();

        for (var i = 0; i < textSegments.Length; i++)
        {
            var isLast = i == textSegments.Length - 1;
            chunks.Add(new ChatCompletionChunk(
                Content: textSegments[i],
                FinishReason: isLast ? "stop" : null,
                Model: DefaultModel));
        }

        return chunks;
    }

    /// <summary>
    /// Creates a chat completion response with cache usage information.
    /// </summary>
    public static ChatCompletionResponse CachedChat(
        string content,
        int cachedInputTokens,
        int? cacheCreationInputTokens = null,
        string model = DefaultModel,
        int promptTokens = 10,
        int completionTokens = 5) =>
        new(Content: content, Model: model, PromptTokens: promptTokens, CompletionTokens: completionTokens,
            CachedInputTokens: cachedInputTokens, CacheCreationInputTokens: cacheCreationInputTokens);

    /// <summary>
    /// Creates a successful embedding response.
    /// </summary>
    public static EmbeddingResponse Embedding(
        float[]? vector = null,
        string model = DefaultModel,
        int totalTokens = 8) =>
        new(
            Embeddings: [vector ?? [0.1f, 0.2f, 0.3f]],
            Base64Embeddings: null,
            Model: model,
            TotalTokens: totalTokens);

    /// <summary>
    /// Creates an embedding response with multiple vectors.
    /// </summary>
    public static EmbeddingResponse Embeddings(
        IReadOnlyList<float[]> vectors,
        string model = DefaultModel,
        int totalTokens = 16) =>
        new(
            Embeddings: vectors,
            Base64Embeddings: null,
            Model: model,
            TotalTokens: totalTokens);

    /// <summary>
    /// Creates an embedding error response.
    /// </summary>
    public static EmbeddingResponse EmbeddingError(string errorMessage) =>
        EmbeddingResponse.Error(errorMessage);

    /// <summary>
    /// Creates a successful rerank response from (index, score) pairs, in the order given.
    /// </summary>
    public static RerankResponse Rerank(
        params (int Index, double RelevanceScore)[] results) =>
        new(
            Results: results.Select(r => new RerankResult(r.Index, r.RelevanceScore)).ToList(),
            Model: DefaultModel,
            SearchUnits: 1);

    /// <summary>
    /// Creates a successful rerank response that ranks the given number of documents in
    /// their original order with descending scores.
    /// </summary>
    public static RerankResponse Rerank(
        int documentCount,
        string model = DefaultModel) =>
        new(
            Results: Enumerable.Range(0, documentCount)
                .Select(i => new RerankResult(i, 1.0 - (i * 0.1)))
                .ToList(),
            Model: model,
            SearchUnits: 1);

    /// <summary>
    /// Creates a rerank error response.
    /// </summary>
    public static RerankResponse RerankError(string errorMessage) =>
        RerankResponse.Error(errorMessage);
}
