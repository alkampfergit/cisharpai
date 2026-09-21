using Cisharpai.Models;

namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Generates a broader, more abstract "step-back" question to retrieve supporting context
/// that the specific query might miss. Returns the step-back query (and optionally the original).
/// </summary>
public sealed class StepBackTransformer : IQueryTransformer
{
    private const string DefaultSystemPrompt =
        "You are a search query abstraction assistant. Given a specific question, generate " +
        "a single broader, more general question that captures the underlying concept or " +
        "principle. The step-back question should retrieve foundational or background " +
        "information that helps answer the original specific question. Output ONLY the " +
        "step-back question, nothing else.";

    private const double DefaultTemperature = 0.0;

    private readonly IChatCompletionClient _client;
    private readonly QueryTransformerOptions _options;
    private readonly string _systemPrompt;
    private readonly bool _includeOriginal;

    public StepBackTransformer(
        IChatCompletionClient client,
        QueryTransformerOptions? options = null,
        string? systemPrompt = null,
        bool includeOriginal = true)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _options = (options ?? new QueryTransformerOptions()).Snapshot();
        _systemPrompt = systemPrompt ?? DefaultSystemPrompt;
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
            Temperature: _options.Temperature ?? DefaultTemperature);

        var response = await _client.GetChatCompletionAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            throw new InvalidOperationException(
                $"Step-back generation failed: {response.ErrorMessage}");

        var stepBack = response.Content.Trim();
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
