using System.Text;
using Cisharpai.Models;
using Cisharpai.Rag.QueryTransformation;

namespace Cisharpai.Rag.Pipeline;

/// <summary>
/// Rewrites a follow-up query into a standalone search query using conversation history.
/// Delegates to <see cref="IChatCompletionClient"/> for the LLM call.
/// </summary>
public sealed class ConversationQueryRewriter
{
    private const string DefaultSystemPrompt =
        "You are a query rewriting assistant. Given a conversation history and a follow-up " +
        "question, reformulate the follow-up question into a standalone search query that " +
        "captures the full intent without requiring the conversation context. " +
        "Output ONLY the rewritten query, nothing else.";

    private readonly IChatCompletionClient _client;
    private readonly string _systemPrompt;
    private readonly string? _model;
    private readonly double _temperature;

    public ConversationQueryRewriter(
        IChatCompletionClient client,
        string? systemPrompt = null,
        string? model = null,
        double temperature = 0.0)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _systemPrompt = systemPrompt ?? DefaultSystemPrompt;
        _model = model;
        _temperature = temperature;
    }

    public async Task<string> RewriteAsync(
        string query,
        IReadOnlyList<LlmMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(conversationHistory);

        if (conversationHistory.Count == 0)
            return query;

        var historyBlock = new StringBuilder();
        foreach (var msg in conversationHistory)
        {
            historyBlock.AppendLine($"{msg.Role}: {msg.Content}");
        }

        var userPrompt = $"Conversation history:\n{historyBlock}\nFollow-up question: {query}";

        var messages = new List<LlmMessage>
        {
            new(LlmRole.System, _systemPrompt),
            new(LlmRole.User, userPrompt)
        };

        var request = new ChatCompletionRequest(
            Messages: messages,
            Model: _model,
            Temperature: _temperature);

        var response = await _client.GetChatCompletionAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Content))
            return query;

        return response.Content.Trim();
    }
}
