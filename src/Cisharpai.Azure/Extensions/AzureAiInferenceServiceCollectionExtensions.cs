using Azure.Core;
using Cisharpai.Azure.AzureAiInference;
using Cisharpai.Azure.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
                options,
                sp.GetService<ILoggerFactory>()));

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
                options,
                sp.GetService<ILoggerFactory>()));

        services.AddSingleton<IEmbeddingClient>(sp =>
            sp.GetRequiredService<AzureAiInferenceEmbeddingClient>());

        return builder;
    }

    public static IHttpClientBuilder AddAzureAiInferenceChatCompletion(
        this IServiceCollection services,
        string key,
        Action<AzureAiInferenceClientOptions> configure,
        TokenCredential? credential = null)
    {
        var options = new AzureAiInferenceClientOptions();
        configure(options);
        options.Validate();
        options.ValidateAuthentication(credential is not null);

        var clientName = $"{nameof(AzureAiInferenceChatCompletionClient)}_{key}";

        var builder = services.AddHttpClient(clientName, client =>
            {
                client.BaseAddress = new Uri(options.Endpoint);
            })
            .AddHttpMessageHandler(() => new AzureAuthenticationHandler(options, credential));

        builder.AddCisharpaiResilienceHandler();

        services.AddKeyedTransient<AzureAiInferenceChatCompletionClient>(key, (sp, _) =>
            new AzureAiInferenceChatCompletionClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(clientName),
                options,
                sp.GetService<ILoggerFactory>()));

        services.AddKeyedSingleton<IChatCompletionClient>(key, (sp, k) =>
            sp.GetRequiredKeyedService<AzureAiInferenceChatCompletionClient>(k));

        return builder;
    }

    public static IHttpClientBuilder AddAzureAiInferenceEmbeddings(
        this IServiceCollection services,
        string key,
        Action<AzureAiInferenceClientOptions> configure,
        TokenCredential? credential = null)
    {
        var options = new AzureAiInferenceClientOptions();
        configure(options);
        options.Validate();
        options.ValidateAuthentication(credential is not null);

        var clientName = $"{nameof(AzureAiInferenceEmbeddingClient)}_{key}";

        var builder = services.AddHttpClient(clientName, client =>
            {
                client.BaseAddress = new Uri(options.Endpoint);
            })
            .AddHttpMessageHandler(() => new AzureAuthenticationHandler(options, credential));

        builder.AddCisharpaiResilienceHandler();

        services.AddKeyedTransient<AzureAiInferenceEmbeddingClient>(key, (sp, _) =>
            new AzureAiInferenceEmbeddingClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(clientName),
                options,
                sp.GetService<ILoggerFactory>()));

        services.AddKeyedSingleton<IEmbeddingClient>(key, (sp, k) =>
            sp.GetRequiredKeyedService<AzureAiInferenceEmbeddingClient>(k));

        return builder;
    }
}
