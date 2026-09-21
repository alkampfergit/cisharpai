using Cisharpai.Models;

namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Generates N query variants from the original query. Each variant is retrieved independently
/// and results are fused (e.g. via <see cref="RankFusion.ReciprocalRank"/>). Returns the
/// original query plus the generated variants.
/// </summary>
public sealed class MultiQueryExpander : IQueryTransformer
{
    private const string DefaultSystemPromptTemplate =
        "You are a search query expansion assistant. Given a user question, generate " +
        "exactly {0} alternative versions of the question that approach the topic from " +
        "different angles. Each variant should be a complete, self-contained search query " +
        "that could retrieve relevant documents independently. Output one query per line, " +
        "no numbering, no bullets, no extra text.";

    private readonly IChatCompletionClient _client;
    private readonly QueryTransformerOptions _options;
    private readonly int _variantCount;
    private readonly string _systemPrompt;
    private readonly bool _includeOriginal;

    public MultiQueryExpander(
        IChatCompletionClient client,
        int variantCount = 3,
        QueryTransformerOptions? options = null,
        string? systemPrompt = null,
        bool includeOriginal = true)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentOutOfRangeException.ThrowIfLessThan(variantCount, 1);

        _client = client;
        _variantCount = variantCount;
        _options = options ?? new QueryTransformerOptions { Temperature = 0.7 };
        _systemPrompt = systemPrompt ?? string.Format(DefaultSystemPromptTemplate, variantCount);
        _includeOriginal = includeOriginal;
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
            Temperature: _options.Temperature);

        var response = await _client.GetChatCompletionAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            throw new InvalidOperationException(
                $"Multi-query expansion failed: {response.ErrorMessage}");

        var variants = response.Content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var result = new List<string>(_variantCount + 1);
        if (_includeOriginal)
            result.Add(query);
        result.AddRange(variants);
        return result;
    }
}
