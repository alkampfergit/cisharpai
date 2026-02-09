using Azure.Core;
using Cisharpai.Azure.AzureOpenAi;
using Cisharpai.Azure.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Azure;

public static class AzureOpenAiServiceCollectionExtensions
{
    /// <summary>
    /// Adds Azure OpenAI chat completion client to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure client options.</param>
    /// <param name="credential">Optional Azure AD token credential for authentication.</param>
    /// <returns>The HTTP client builder for further configuration.</returns>
    public static IHttpClientBuilder AddAzureOpenAiClient(
        this IServiceCollection services,
        Action<AzureOpenAiClientOptions> configure,
        TokenCredential? credential = null)
    {
        var options = new AzureOpenAiClientOptions();
        configure(options);
        options.Validate();
        options.ValidateAuthentication(credential is not null);

        services.AddSingleton(options);

        if (credential is not null)
            services.AddSingleton(credential);

        var builder = services.AddHttpClient<AzureOpenAiChatCompletionClient>(client =>
            {
                client.BaseAddress = new Uri(options.Endpoint);
            })
            .AddHttpMessageHandler(() => new AzureAuthenticationHandler(options, credential));

        builder.AddCisharpaiResilienceHandler();

        services.AddSingleton<IChatCompletionClient>(sp =>
            sp.GetRequiredService<AzureOpenAiChatCompletionClient>());

        return builder;
    }

    /// <summary>
    /// Adds Azure OpenAI embedding client to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure client options.</param>
    /// <param name="credential">Optional Azure AD token credential for authentication.</param>
    /// <returns>The HTTP client builder for further configuration.</returns>
    public static IHttpClientBuilder AddAzureOpenAiEmbeddingClient(
        this IServiceCollection services,
        Action<AzureOpenAiClientOptions> configure,
        TokenCredential? credential = null)
    {
        var options = new AzureOpenAiClientOptions();
        configure(options);
        options.Validate();
        options.ValidateAuthentication(credential is not null);

        services.AddSingleton(options);

        if (credential is not null)
            services.AddSingleton(credential);

        var builder = services.AddHttpClient<AzureOpenAiEmbeddingClient>(client =>
            {
                client.BaseAddress = new Uri(options.Endpoint);
            })
            .AddHttpMessageHandler(() => new AzureAuthenticationHandler(options, credential));

        builder.AddCisharpaiResilienceHandler();

        services.AddSingleton<IEmbeddingClient>(sp =>
            sp.GetRequiredService<AzureOpenAiEmbeddingClient>());

        return builder;
    }
}
