using Cisharpai.Models;

namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Hypothetical Document Embeddings (HyDE): generates a hypothetical answer to the query,
/// which is then embedded instead of (or alongside) the original query. The hypothesis
/// tends to be closer in embedding space to real relevant documents than the raw question.
/// Returns one or more hypothetical documents; the caller should embed these for retrieval.
/// </summary>
public sealed class HydeTransformer : IQueryTransformer
{
    private const string DefaultSystemPrompt =
        "You are a helpful assistant. Given a question, write a short passage that " +
        "directly answers it, as if it were an excerpt from a relevant document. " +
        "The passage should be factual in tone and specific enough to match real documents " +
        "on the topic. Do not include any preamble or meta-commentary — output only the " +
        "passage text.";

    private const double DefaultTemperature = 0.7;

    private readonly IChatCompletionClient _client;
    private readonly QueryTransformerOptions _options;
    private readonly string _systemPrompt;
    private readonly int _hypothesisCount;
    private readonly bool _includeOriginal;

    public HydeTransformer(
        IChatCompletionClient client,
        int hypothesisCount = 1,
        QueryTransformerOptions? options = null,
        string? systemPrompt = null,
        bool includeOriginal = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentOutOfRangeException.ThrowIfLessThan(hypothesisCount, 1);

        _client = client;
        _hypothesisCount = hypothesisCount;
        _options = (options ?? new QueryTransformerOptions()).Snapshot();
        _systemPrompt = systemPrompt ?? DefaultSystemPrompt;
        _includeOriginal = includeOriginal;
    }

    public async Task<IReadOnlyList<string>> TransformAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var result = new List<string>(_hypothesisCount + 1);
        if (_includeOriginal)
            result.Add(query);

        for (var i = 0; i < _hypothesisCount; i++)
        {
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
                    $"HyDE generation failed: {response.ErrorMessage}");

            var hypothesis = response.Content.Trim();
            if (!string.IsNullOrWhiteSpace(hypothesis))
                result.Add(hypothesis);
        }

        if (result.Count == 0)
            result.Add(query);

        return result;
    }
}
