using Cisharpai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Cohere;

public static class CohereServiceCollectionExtensions
{
    public static IHttpClientBuilder AddCohereEmbeddingClient(
        this IServiceCollection services,
        Action<CohereClientOptions> configure)
    {
        var options = new CohereClientOptions();
        configure(options);

        var builder = services.AddHttpClient<CohereEmbeddingClient>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler(() => new CohereAuthenticationHandler(options));

        builder.AddCisharpaiResilienceHandler();

        services.AddTransient(sp =>
            new CohereEmbeddingClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(CohereEmbeddingClient).Name),
                options,
                sp.GetService<ILoggerFactory>()));

        services.AddSingleton<IEmbeddingClient>(sp =>
            sp.GetRequiredService<CohereEmbeddingClient>());

        return builder;
    }

    public static IHttpClientBuilder AddCohereChatClient(
        this IServiceCollection services,
        Action<CohereClientOptions> configure)
    {
        var options = new CohereClientOptions();
        configure(options);

        var builder = services.AddHttpClient<CohereChatCompletionClient>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler(() => new CohereAuthenticationHandler(options));

        builder.AddCisharpaiResilienceHandler();

        services.AddTransient(sp =>
            new CohereChatCompletionClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(CohereChatCompletionClient).Name),
                options,
                sp.GetService<ILoggerFactory>()));

        services.AddSingleton<IChatCompletionClient>(sp =>
            sp.GetRequiredService<CohereChatCompletionClient>());

        return builder;
    }

    public static IHttpClientBuilder AddCohereEmbeddingClient(
        this IServiceCollection services,
        string key,
        Action<CohereClientOptions> configure)
    {
        var options = new CohereClientOptions();
        configure(options);

        var clientName = $"{nameof(CohereEmbeddingClient)}_{key}";

        var builder = services.AddHttpClient(clientName, client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler(() => new CohereAuthenticationHandler(options));

        builder.AddCisharpaiResilienceHandler();

        services.AddKeyedTransient<CohereEmbeddingClient>(key, (sp, _) =>
            new CohereEmbeddingClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(clientName),
                options,
                sp.GetService<ILoggerFactory>()));

        services.AddKeyedSingleton<IEmbeddingClient>(key, (sp, k) =>
            sp.GetRequiredKeyedService<CohereEmbeddingClient>(k));

        return builder;
    }

    public static IHttpClientBuilder AddCohereChatClient(
        this IServiceCollection services,
        string key,
        Action<CohereClientOptions> configure)
    {
        var options = new CohereClientOptions();
        configure(options);

        var clientName = $"{nameof(CohereChatCompletionClient)}_{key}";

        var builder = services.AddHttpClient(clientName, client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler(() => new CohereAuthenticationHandler(options));

        builder.AddCisharpaiResilienceHandler();

        services.AddKeyedTransient<CohereChatCompletionClient>(key, (sp, _) =>
            new CohereChatCompletionClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(clientName),
                options,
                sp.GetService<ILoggerFactory>()));

        services.AddKeyedSingleton<IChatCompletionClient>(key, (sp, k) =>
            sp.GetRequiredKeyedService<CohereChatCompletionClient>(k));

        return builder;
    }
}
