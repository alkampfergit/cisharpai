using Azure.Core;
using Cisharpai.Azure.AzureAiInference;
using Cisharpai.Azure.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Azure;

public static class AzureAiInferenceServiceCollectionExtensions
{
    /// <summary>
    /// Adds Azure AI Inference chat completion client to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure client options.</param>
    /// <param name="credential">Optional Azure AD token credential for authentication.</param>
    /// <returns>The HTTP client builder for further configuration.</returns>
    public static IHttpClientBuilder AddAzureAiInferenceChatCompletion(
        this IServiceCollection services,
        Action<AzureAiInferenceClientOptions> configure,
        TokenCredential? credential = null)
    {
        var options = new AzureAiInferenceClientOptions();
        configure(options);
        options.Validate();
        options.ValidateAuthentication(credential is not null);

        var builder = services.AddHttpClient<AzureAiInferenceChatCompletionClient>(client =>
            {
                client.BaseAddress = new Uri(options.Endpoint);
            })
            .AddHttpMessageHandler(() => new AzureAuthenticationHandler(options, credential));

        builder.AddCisharpaiResilienceHandler();

        services.AddTransient(sp =>
            new AzureAiInferenceChatCompletionClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(AzureAiInferenceChatCompletionClient).Name),
                options));

        services.AddSingleton<IChatCompletionClient>(sp =>
            sp.GetRequiredService<AzureAiInferenceChatCompletionClient>());

        return builder;
    }

    /// <summary>
    /// Adds Azure AI Inference embedding client to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure client options.</param>
    /// <param name="credential">Optional Azure AD token credential for authentication.</param>
    /// <returns>The HTTP client builder for further configuration.</returns>
    public static IHttpClientBuilder AddAzureAiInferenceEmbeddings(
        this IServiceCollection services,
        Action<AzureAiInferenceClientOptions> configure,
        TokenCredential? credential = null)
    {
        var options = new AzureAiInferenceClientOptions();
        configure(options);
        options.Validate();
        options.ValidateAuthentication(credential is not null);

        var builder = services.AddHttpClient<AzureAiInferenceEmbeddingClient>(client =>
            {
                client.BaseAddress = new Uri(options.Endpoint);
            })
            .AddHttpMessageHandler(() => new AzureAuthenticationHandler(options, credential));

        builder.AddCisharpaiResilienceHandler();

        services.AddTransient(sp =>
            new AzureAiInferenceEmbeddingClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(AzureAiInferenceEmbeddingClient).Name),
                options));

        services.AddSingleton<IEmbeddingClient>(sp =>
            sp.GetRequiredService<AzureAiInferenceEmbeddingClient>());

        return builder;
    }
}
