namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Generates a broader, more abstract "step-back" question to retrieve supporting context
/// that the specific query might miss. Returns the step-back query (and optionally the original).
/// </summary>
public sealed class StepBackTransformer : QueryTransformerBase
{
    private const string DefaultSystemPrompt =
        "You are a search query abstraction assistant. Given a specific question, generate " +
        "a single broader, more general question that captures the underlying concept or " +
        "principle. The step-back question should retrieve foundational or background " +
        "information that helps answer the original specific question. Output ONLY the " +
        "step-back question, nothing else.";

    private readonly bool _includeOriginal;

    protected override double DefaultTemperature => 0.0;

    public StepBackTransformer(
        IChatCompletionClient client,
        QueryTransformerOptions? options = null,
        string? systemPrompt = null,
        bool includeOriginal = true)
        : base(client, options, systemPrompt, DefaultSystemPrompt)
    {
        _includeOriginal = includeOriginal;
    }

    public override async Task<IReadOnlyList<string>> TransformAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var stepBack = await CallLlmAsync(query, "Step-back generation failed", cancellationToken)
            .ConfigureAwait(false);

        var result = new List<string>(2);

        if (_includeOriginal)
            result.Add(query);

        if (!string.IsNullOrWhiteSpace(stepBack))
            result.Add(stepBack);

        if (result.Count == 0)
            result.Add(query);

        return result;
    }
}
