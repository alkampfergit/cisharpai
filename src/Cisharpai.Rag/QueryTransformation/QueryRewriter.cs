using Cisharpai.Models;

namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Reformulates the user query for better retrieval — removes conversational filler,
/// clarifies intent, and produces a clean search query. Returns exactly one output.
/// </summary>
public sealed class QueryRewriter : IQueryTransformer
{
    private const string DefaultSystemPrompt =
        "You are a query rewriting assistant. Your task is to reformulate the user's " +
        "question into a clear, concise search query optimized for document retrieval. " +
        "Remove conversational filler, resolve pronouns where possible, and clarify " +
        "ambiguous intent. Output ONLY the rewritten query, nothing else.";

    private const double DefaultTemperature = 0.0;

    private readonly IChatCompletionClient _client;
    private readonly QueryTransformerOptions _options;
    private readonly string _systemPrompt;

    public QueryRewriter(
        IChatCompletionClient client,
        QueryTransformerOptions? options = null,
        string? systemPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _options = (options ?? new QueryTransformerOptions()).Snapshot();
        _systemPrompt = systemPrompt ?? DefaultSystemPrompt;
    }

    public async Task<IReadOnlyList<string>> TransformAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var request = new ChatCompletionRequest(
            Messages: [
                new LlmMessage(LlmRole.System, _systemPrompt),
                new LlmMessage(LlmRole.User, query)
            ],
            Model: _options.Model,
            Temperature: _options.Temperature ?? DefaultTemperature);

        var response = await _client.GetChatCompletionAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            throw new InvalidOperationException(
                $"Query rewrite failed: {response.ErrorMessage}");

        var rewritten = response.Content.Trim();
        return string.IsNullOrWhiteSpace(rewritten)
            ? [query]
            : [rewritten];
    }
}
