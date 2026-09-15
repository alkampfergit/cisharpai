using Cisharpai.OpenAi;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Rag.OpenAi;

/// <summary>
/// <see cref="IHostedRetrievalFeature"/> implementation backed by OpenAI's <c>file_search</c>
/// tool on the Responses API. Each <see cref="ForStore"/> call returns an independent
/// <see cref="IRetriever"/> bound to the specified vector store.
/// <para>
/// Inject into an <see cref="OpenAiChatCompletionClient"/>'s feature collection via
/// <c>client.Features.Set&lt;IHostedRetrievalFeature&gt;(feature)</c> so that
/// <c>Features.Get&lt;IHostedRetrievalFeature&gt;()</c> resolves it through the
/// standard discovery pattern.
/// </para>
/// </summary>
public sealed class OpenAiHostedRetrievalFeature : IHostedRetrievalFeature
{
    private readonly LlmHttpClient _client;
    private readonly OpenAiClientOptions _options;
    private readonly ILoggerFactory? _loggerFactory;

    public OpenAiHostedRetrievalFeature(HttpClient httpClient, OpenAiClientOptions options, ILoggerFactory? loggerFactory = null)
    {
        _client = new LlmHttpClient(httpClient, logger: loggerFactory?.CreateLogger<LlmHttpClient>());
        _options = options;
        _loggerFactory = loggerFactory;
    }

    public IRetriever ForStore(string vectorStoreId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vectorStoreId);
        return new OpenAiFileSearchRetriever(
            _client,
            _options,
            vectorStoreId,
            _loggerFactory?.CreateLogger<OpenAiFileSearchRetriever>());
    }
}
