using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cisharpai.OpenAi;

public sealed class OpenAiClientFactoryProvider : IClientFactoryProvider
{
    internal const string ChatHttpClientName = "CisharpaiFactory_OpenAi_Chat";
    internal const string EmbeddingHttpClientName = "CisharpaiFactory_OpenAi_Embedding";

    public CisharpaiProvider Provider => CisharpaiProvider.OpenAi;

    public bool SupportsChatCompletion => true;

    public bool SupportsEmbedding => true;

    public CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (configuration is not OpenAiClientConfiguration config)
        {
            return CisharpaiClientFactoryResult<IChatCompletionClient>.Failure(
                $"Expected OpenAiClientConfiguration for provider OpenAi, got {configuration.GetType().Name}.");
        }

        var options = MapToOptions(config);
        var httpClient = CreateHttpClient(serviceProvider, ChatHttpClientName, config);
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        var client = new OpenAiChatCompletionClient(httpClient, options, loggerFactory);
        return CisharpaiClientFactoryResult<IChatCompletionClient>.Success(client);
    }

    public CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (configuration is not OpenAiClientConfiguration config)
        {
            return CisharpaiClientFactoryResult<IEmbeddingClient>.Failure(
                $"Expected OpenAiClientConfiguration for provider OpenAi, got {configuration.GetType().Name}.");
        }

        var options = MapToOptions(config);
        var httpClient = CreateHttpClient(serviceProvider, EmbeddingHttpClientName, config);
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        var client = new OpenAiEmbeddingClient(httpClient, options, loggerFactory);
        return CisharpaiClientFactoryResult<IEmbeddingClient>.Success(client);
    }

    private static HttpClient CreateHttpClient(
        IServiceProvider serviceProvider,
        string clientName,
        OpenAiClientConfiguration config)
    {
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(clientName);
        httpClient.BaseAddress = new Uri(config.BaseUrl);
        httpClient.Timeout = TimeSpan.FromMinutes(2);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config.ApiKey);
        if (!string.IsNullOrWhiteSpace(config.Organization))
            httpClient.DefaultRequestHeaders.Add("OpenAI-Organization", config.Organization);
        return httpClient;
    }

    private static OpenAiClientOptions MapToOptions(OpenAiClientConfiguration config) =>
        new()
        {
            ApiKey = config.ApiKey,
            BaseUrl = config.BaseUrl,
            DefaultModel = config.DefaultModel,
            Organization = config.Organization,
            ReasoningEffort = config.ReasoningEffort,
            TextVerbosity = config.TextVerbosity
        };
}
