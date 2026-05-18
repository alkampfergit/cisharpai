using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Azure.AzureOpenAi;

public sealed class AzureOpenAiClientFactoryProvider : IClientFactoryProvider
{
    internal const string ChatHttpClientName = "CisharpaiFactory_AzureOpenAi_Chat";
    internal const string EmbeddingHttpClientName = "CisharpaiFactory_AzureOpenAi_Embedding";

    public CisharpaiProvider Provider => CisharpaiProvider.AzureOpenAi;

    public bool SupportsChatCompletion => true;

    public bool SupportsEmbedding => true;

    public CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (configuration is not AzureOpenAiClientConfiguration config)
        {
            return CisharpaiClientFactoryResult<IChatCompletionClient>.Failure(
                $"Expected AzureOpenAiClientConfiguration for provider AzureOpenAi, got {configuration.GetType().Name}.");
        }

        var options = MapToOptions(config);
        var httpClient = CreateHttpClient(serviceProvider, ChatHttpClientName, config);
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        var client = new AzureOpenAiChatCompletionClient(httpClient, options, loggerFactory);
        return CisharpaiClientFactoryResult<IChatCompletionClient>.Success(client);
    }

    public CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (configuration is not AzureOpenAiClientConfiguration config)
        {
            return CisharpaiClientFactoryResult<IEmbeddingClient>.Failure(
                $"Expected AzureOpenAiClientConfiguration for provider AzureOpenAi, got {configuration.GetType().Name}.");
        }

        var options = MapToOptions(config);
        var httpClient = CreateHttpClient(serviceProvider, EmbeddingHttpClientName, config);
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        var client = new AzureOpenAiEmbeddingClient(httpClient, options, loggerFactory);
        return CisharpaiClientFactoryResult<IEmbeddingClient>.Success(client);
    }

    private static HttpClient CreateHttpClient(
        IServiceProvider serviceProvider,
        string clientName,
        AzureOpenAiClientConfiguration config)
    {
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(clientName);
        httpClient.BaseAddress = new Uri(config.Endpoint);
        httpClient.DefaultRequestHeaders.Add("api-key", config.ApiKey);
        return httpClient;
    }

    private static AzureOpenAiClientOptions MapToOptions(AzureOpenAiClientConfiguration config) =>
        new()
        {
            Endpoint = config.Endpoint,
            ApiKey = config.ApiKey,
            ApiVersion = config.ApiVersion,
            DeploymentName = config.DeploymentName,
            DefaultModel = config.DefaultModel,
            ModelName = config.ModelName,
            ReasoningEffort = config.ReasoningEffort,
            TextVerbosity = config.TextVerbosity
        };
}
