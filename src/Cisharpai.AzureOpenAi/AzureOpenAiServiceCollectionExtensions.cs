using Azure.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.AzureOpenAi;

public static class AzureOpenAiServiceCollectionExtensions
{
    public static IHttpClientBuilder AddAzureOpenAiClient(
        this IServiceCollection services,
        Action<AzureOpenAiClientOptions> configure,
        TokenCredential? credential = null)
    {
        var options = new AzureOpenAiClientOptions();
        configure(options);

        services.AddSingleton(options);

        if (credential is not null)
            services.AddSingleton(credential);

        services.AddTransient<AzureOpenAiAuthenticationHandler>();

        var builder = services.AddHttpClient<AzureOpenAiChatCompletionClient>(client =>
            {
                client.BaseAddress = new Uri(options.Endpoint);
            })
            .AddHttpMessageHandler<AzureOpenAiAuthenticationHandler>();

        builder.AddStandardResilienceHandler();

        services.AddSingleton<IChatCompletionClient>(sp =>
            sp.GetRequiredService<AzureOpenAiChatCompletionClient>());

        return builder;
    }
}
