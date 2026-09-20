using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Cohere;

public sealed class CohereClientFactoryProvider : IClientFactoryProvider
{
    internal const string ChatHttpClientName = "CisharpaiFactory_Cohere_Chat";
    internal const string EmbeddingHttpClientName = "CisharpaiFactory_Cohere_Embedding";
    internal const string RerankHttpClientName = "CisharpaiFactory_Cohere_Rerank";

    public CisharpaiProvider Provider => CisharpaiProvider.Cohere;

    public bool SupportsChatCompletion => true;

    public bool SupportsEmbedding => true;

    public bool SupportsReranking => true;

    public CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (configuration is not CohereClientConfiguration config)
        {
            return CisharpaiClientFactoryResult<IChatCompletionClient>.Failure(
                $"Expected CohereClientConfiguration for provider Cohere, got {configuration.GetType().Name}.");
        }

        var options = MapToOptions(config);
        var httpClient = CreateHttpClient(serviceProvider, ChatHttpClientName, config);
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        var client = new CohereChatCompletionClient(httpClient, options, loggerFactory);
        return CisharpaiClientFactoryResult<IChatCompletionClient>.Success(client);
    }

    public CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (configuration is not CohereClientConfiguration config)
        {
            return CisharpaiClientFactoryResult<IEmbeddingClient>.Failure(
                $"Expected CohereClientConfiguration for provider Cohere, got {configuration.GetType().Name}.");
        }

        var options = MapToOptions(config);
        var httpClient = CreateHttpClient(serviceProvider, EmbeddingHttpClientName, config);
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        var client = new CohereEmbeddingClient(httpClient, options, loggerFactory);
        return CisharpaiClientFactoryResult<IEmbeddingClient>.Success(client);
    }

    public CisharpaiClientFactoryResult<IRerankerClient> CreateRerankerClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (configuration is not CohereClientConfiguration config)
        {
            return CisharpaiClientFactoryResult<IRerankerClient>.Failure(
                $"Expected CohereClientConfiguration for provider Cohere, got {configuration.GetType().Name}.");
        }

        var options = MapToOptions(config);
        var httpClient = CreateHttpClient(serviceProvider, RerankHttpClientName, config);
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        var client = new CohereRerankerClient(httpClient, options, loggerFactory);
        return CisharpaiClientFactoryResult<IRerankerClient>.Success(client);
    }

    private static HttpClient CreateHttpClient(
        IServiceProvider serviceProvider,
        string clientName,
        CohereClientConfiguration config)
    {
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(clientName);
        httpClient.BaseAddress = new Uri(config.BaseUrl);
        httpClient.Timeout = TimeSpan.FromMinutes(2);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config.ApiKey);
        return httpClient;
    }

    private static CohereClientOptions MapToOptions(CohereClientConfiguration config) =>
        new()
        {
            ApiKey = config.ApiKey,
            BaseUrl = config.BaseUrl,
            DefaultModel = config.DefaultModel
        };
}
