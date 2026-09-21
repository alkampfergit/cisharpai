namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Generates N query variants from the original query. Each variant is retrieved independently
/// and results are fused (e.g. via <see cref="RankFusion.ReciprocalRank"/>). Returns the
/// original query plus the generated variants.
/// </summary>
public sealed class MultiQueryExpander : QueryTransformerBase
{
    private const string DefaultSystemPromptTemplate =
        "You are a search query expansion assistant. Given a user question, generate " +
        "exactly {0} alternative versions of the question that approach the topic from " +
        "different angles. Each variant should be a complete, self-contained search query " +
        "that could retrieve relevant documents independently. Output one query per line, " +
        "no numbering, no bullets, no extra text.";

    private readonly int _variantCount;
    private readonly bool _includeOriginal;

    protected override double DefaultTemperature => 0.7;

    public MultiQueryExpander(
        IChatCompletionClient client,
        int variantCount = 3,
        QueryTransformerOptions? options = null,
        string? systemPrompt = null,
        bool includeOriginal = true)
        : base(client, options, systemPrompt, string.Format(DefaultSystemPromptTemplate, variantCount))
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(variantCount, 1);
        _variantCount = variantCount;
        _includeOriginal = includeOriginal;
    }

    public override async Task<IReadOnlyList<string>> TransformAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var content = await CallLlmAsync(query, "Multi-query expansion failed", cancellationToken)
            .ConfigureAwait(false);

        var variants = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(_variantCount)
            .ToList();

        var result = new List<string>(_variantCount + 1);
        if (_includeOriginal)
            result.Add(query);
        result.AddRange(variants);
        return result;
    }
}
