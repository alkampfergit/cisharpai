using Cisharpai.Models;

namespace Cisharpai.Rag.QueryTransformation;

/// <summary>
/// Base class for LLM-backed query transformers. Handles client/options storage
/// and the common request-response cycle so subclasses only define post-processing.
/// </summary>
public abstract class QueryTransformerBase : IQueryTransformer
{
    private readonly IChatCompletionClient _client;
    private readonly QueryTransformerOptions _options;
    private readonly string _systemPrompt;

    protected abstract double DefaultTemperature { get; }

    protected QueryTransformerBase(
        IChatCompletionClient client,
        QueryTransformerOptions? options,
        string? systemPrompt,
        string defaultSystemPrompt)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _options = (options ?? new QueryTransformerOptions()).Snapshot();
        _systemPrompt = systemPrompt ?? defaultSystemPrompt;
    }

    protected async Task<string> CallLlmAsync(
        string query,
        string errorContext,
        CancellationToken cancellationToken)
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
                $"{errorContext}: {response.ErrorMessage}");

        return response.Content.Trim();
    }

    public abstract Task<IReadOnlyList<string>> TransformAsync(
        string query,
        CancellationToken cancellationToken = default);
}
