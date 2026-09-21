namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Reformulates the user query for better retrieval — removes conversational filler,
/// clarifies intent, and produces a clean search query. Returns exactly one output.
/// </summary>
public sealed class QueryRewriter : QueryTransformerBase
{
    private const string DefaultSystemPrompt =
        "You are a query rewriting assistant. Your task is to reformulate the user's " +
        "question into a clear, concise search query optimized for document retrieval. " +
        "Remove conversational filler, resolve pronouns where possible, and clarify " +
        "ambiguous intent. Output ONLY the rewritten query, nothing else.";

    protected override double DefaultTemperature => 0.0;

    public QueryRewriter(
        IChatCompletionClient client,
        QueryTransformerOptions? options = null,
        string? systemPrompt = null)
        : base(client, options, systemPrompt, DefaultSystemPrompt)
    {
    }

    public override async Task<IReadOnlyList<string>> TransformAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var rewritten = await CallLlmAsync(query, "Query rewrite failed", cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(rewritten)
            ? [query]
            : [rewritten];
    }
}
