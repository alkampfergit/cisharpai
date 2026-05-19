using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Azure.AzureAiInference;

public sealed class AzureAiInferenceClientFactoryProvider : IClientFactoryProvider
{
    internal const string ChatHttpClientName = "CisharpaiFactory_AzureAiInference_Chat";
    internal const string EmbeddingHttpClientName = "CisharpaiFactory_AzureAiInference_Embedding";

    public CisharpaiProvider Provider => CisharpaiProvider.AzureAiInference;

    public bool SupportsChatCompletion => true;

    public bool SupportsEmbedding => true;

    public CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (configuration is not AzureAiInferenceClientConfiguration config)
        {
            return CisharpaiClientFactoryResult<IChatCompletionClient>.Failure(
                $"Expected AzureAiInferenceClientConfiguration for provider AzureAiInference, got {configuration.GetType().Name}.");
        }

        var options = MapToOptions(config);
        var httpClient = CreateHttpClient(serviceProvider, ChatHttpClientName, config);
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        var client = new AzureAiInferenceChatCompletionClient(httpClient, options, loggerFactory);
        return CisharpaiClientFactoryResult<IChatCompletionClient>.Success(client);
    }

    public CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (configuration is not AzureAiInferenceClientConfiguration config)
        {
            return CisharpaiClientFactoryResult<IEmbeddingClient>.Failure(
                $"Expected AzureAiInferenceClientConfiguration for provider AzureAiInference, got {configuration.GetType().Name}.");
        }

        var options = MapToOptions(config);
        var httpClient = CreateHttpClient(serviceProvider, EmbeddingHttpClientName, config);
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        var client = new AzureAiInferenceEmbeddingClient(httpClient, options, loggerFactory);
        return CisharpaiClientFactoryResult<IEmbeddingClient>.Success(client);
    }

    private static HttpClient CreateHttpClient(
        IServiceProvider serviceProvider,
        string clientName,
        AzureAiInferenceClientConfiguration config)
    {
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(clientName);
        httpClient.BaseAddress = new Uri(config.Endpoint);
        httpClient.Timeout = TimeSpan.FromMinutes(2);
        httpClient.DefaultRequestHeaders.Add("api-key", config.ApiKey);
        return httpClient;
    }

    private static AzureAiInferenceClientOptions MapToOptions(AzureAiInferenceClientConfiguration config) =>
        new()
        {
            Endpoint = config.Endpoint,
            ApiKey = config.ApiKey,
            ApiVersion = config.ApiVersion,
            ModelId = config.ModelId
        };
}
