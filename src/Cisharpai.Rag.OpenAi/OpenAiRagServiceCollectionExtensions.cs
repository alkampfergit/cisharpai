using Cisharpai.OpenAi;
using Cisharpai.Rag;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Rag.OpenAi;

public static class OpenAiRagServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IHostedRetrievalFeature"/> backed by OpenAI <c>file_search</c>
    /// and injects it into the <see cref="OpenAiChatCompletionClient"/>'s feature collection
    /// so that <c>Features.Get&lt;IHostedRetrievalFeature&gt;()</c> resolves it.
    /// Call after <see cref="OpenAiServiceCollectionExtensions.AddOpenAiClient"/>.
    /// </summary>
    public static IHttpClientBuilder AddOpenAiHostedRetrieval(
        this IServiceCollection services,
        Action<OpenAiClientOptions> configure)
    {
        var options = new OpenAiClientOptions();
        configure(options);

        var builder = services.AddHttpClient("CisharpaiOpenAiHostedRetrieval", client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler(() => new OpenAiAuthenticationHandler(options));

        builder.AddCisharpaiResilienceHandler();

        services.AddSingleton<IHostedRetrievalFeature>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient("CisharpaiOpenAiHostedRetrieval");
            return new OpenAiHostedRetrievalFeature(httpClient, options, sp.GetService<ILoggerFactory>());
        });

        var clientDescriptor = services.LastOrDefault(d =>
            d.ServiceType == typeof(OpenAiChatCompletionClient) &&
            d.ImplementationFactory is not null);

        if (clientDescriptor is not null)
        {
            var originalFactory = clientDescriptor.ImplementationFactory!;
            services.Remove(clientDescriptor);
            services.Add(new ServiceDescriptor(
                typeof(OpenAiChatCompletionClient),
                sp =>
                {
                    var client = (OpenAiChatCompletionClient)originalFactory(sp);
                    client.Features.Set<IHostedRetrievalFeature>(
                        sp.GetRequiredService<IHostedRetrievalFeature>());
                    return client;
                },
                clientDescriptor.Lifetime));
        }

        return builder;
    }
}
