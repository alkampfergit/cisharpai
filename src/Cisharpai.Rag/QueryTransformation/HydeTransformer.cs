namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Hypothetical Document Embeddings (HyDE): generates a hypothetical answer to the query,
/// which is then embedded instead of (or alongside) the original query. The hypothesis
/// tends to be closer in embedding space to real relevant documents than the raw question.
/// Returns one or more hypothetical documents; the caller should embed these for retrieval.
/// </summary>
public sealed class HydeTransformer : QueryTransformerBase
{
    private const string DefaultSystemPrompt =
        "You are a helpful assistant. Given a question, write a short passage that " +
        "directly answers it, as if it were an excerpt from a relevant document. " +
        "The passage should be factual in tone and specific enough to match real documents " +
        "on the topic. Do not include any preamble or meta-commentary — output only the " +
        "passage text.";

    private readonly int _hypothesisCount;
    private readonly bool _includeOriginal;

    protected override double DefaultTemperature => 0.7;

    public HydeTransformer(
        IChatCompletionClient client,
        int hypothesisCount = 1,
        QueryTransformerOptions? options = null,
        string? systemPrompt = null,
        bool includeOriginal = false)
        : base(client, options, systemPrompt, DefaultSystemPrompt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(hypothesisCount, 1);
        _hypothesisCount = hypothesisCount;
        _includeOriginal = includeOriginal;
    }

    public override async Task<IReadOnlyList<string>> TransformAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var result = new List<string>(_hypothesisCount + 1);
        if (_includeOriginal)
            result.Add(query);

        for (var i = 0; i < _hypothesisCount; i++)
        {
            var hypothesis = await CallLlmAsync(query, "HyDE generation failed", cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(hypothesis))
                result.Add(hypothesis);
        }

        if (result.Count == 0)
            result.Add(query);

        return result;
    }
}
