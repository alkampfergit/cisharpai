using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Testing;

/// <summary>
/// Static factory methods for creating common fake responses.
/// </summary>
public static class FakeResponses
{
    /// <summary>
    /// Creates a successful chat completion response.
    /// </summary>
    public static ChatCompletionResponse Chat(
        string content,
        string model = "fake-model",
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
        string model = "fake-model") =>
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
                Model: "fake-model",
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
        string model = "fake-model") =>
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
                Model: "fake-model"));
        }

        return chunks;
    }

    /// <summary>
    /// Creates a successful embedding response.
    /// </summary>
    public static EmbeddingResponse Embedding(
        float[]? vector = null,
        string model = "fake-model",
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
        string model = "fake-model",
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
}
