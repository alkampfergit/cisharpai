using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Anthropic;

public sealed class AnthropicClientFactoryProvider : IClientFactoryProvider
{
    internal const string ChatHttpClientName = "CisharpaiFactory_Anthropic_Chat";

    public CisharpaiProvider Provider => CisharpaiProvider.Anthropic;

    public bool SupportsChatCompletion => true;

    public bool SupportsEmbedding => false;

    public CisharpaiClientFactoryResult<IChatCompletionClient> CreateChatCompletionClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        if (configuration is not AnthropicClientConfiguration config)
        {
            return CisharpaiClientFactoryResult<IChatCompletionClient>.Failure(
                $"Expected AnthropicClientConfiguration for provider Anthropic, got {configuration.GetType().Name}.");
        }

        var options = MapToOptions(config);
        var httpClient = CreateHttpClient(serviceProvider, config);
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        var client = new AnthropicChatCompletionClient(httpClient, options, loggerFactory);
        return CisharpaiClientFactoryResult<IChatCompletionClient>.Success(client);
    }

    public CisharpaiClientFactoryResult<IEmbeddingClient> CreateEmbeddingClient(
        IServiceProvider serviceProvider,
        CisharpaiClientConfiguration configuration)
    {
        return CisharpaiClientFactoryResult<IEmbeddingClient>.Failure(
            $"Provider '{Provider}' does not support embedding clients.");
    }

    private static HttpClient CreateHttpClient(
        IServiceProvider serviceProvider,
        AnthropicClientConfiguration config)
    {
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(ChatHttpClientName);
        httpClient.BaseAddress = new Uri(config.BaseUrl);
        httpClient.Timeout = TimeSpan.FromMinutes(2);
        httpClient.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", config.ApiVersion);
        return httpClient;
    }

    private static AnthropicClientOptions MapToOptions(AnthropicClientConfiguration config) =>
        new()
        {
            ApiKey = config.ApiKey,
            BaseUrl = config.BaseUrl,
            ApiVersion = config.ApiVersion,
            DefaultModel = config.DefaultModel
        };
}
