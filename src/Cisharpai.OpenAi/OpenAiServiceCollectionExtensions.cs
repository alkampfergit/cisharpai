using Cisharpai;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.OpenAi;

public static class OpenAiServiceCollectionExtensions
{
    public static IHttpClientBuilder AddOpenAiClient(
        this IServiceCollection services,
        Action<OpenAiClientOptions> configure)
    {
        var options = new OpenAiClientOptions();
        configure(options);

        var builder = services.AddHttpClient<OpenAiChatCompletionClient>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler(() => new OpenAiAuthenticationHandler(options));

        builder.AddCisharpaiResilienceHandler();

        services.AddTransient(sp =>
            new OpenAiChatCompletionClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(OpenAiChatCompletionClient).Name),
                options));

        services.AddSingleton<IChatCompletionClient>(sp =>
            sp.GetRequiredService<OpenAiChatCompletionClient>());

        return builder;
    }

    public static IHttpClientBuilder AddOpenAiEmbeddingClient(
        this IServiceCollection services,
        Action<OpenAiClientOptions> configure)
    {
        var options = new OpenAiClientOptions();
        configure(options);

        var builder = services.AddHttpClient<OpenAiEmbeddingClient>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler(() => new OpenAiAuthenticationHandler(options));

        builder.AddCisharpaiResilienceHandler();

        services.AddTransient(sp =>
            new OpenAiEmbeddingClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(OpenAiEmbeddingClient).Name),
                options));

        services.AddSingleton<IEmbeddingClient>(sp =>
            sp.GetRequiredService<OpenAiEmbeddingClient>());

        return builder;
    }

    public static IHttpClientBuilder AddOpenAiClient(
        this IServiceCollection services,
        string key,
        Action<OpenAiClientOptions> configure)
    {
        var options = new OpenAiClientOptions();
        configure(options);

        var clientName = $"{nameof(OpenAiChatCompletionClient)}_{key}";

        var builder = services.AddHttpClient(clientName, client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler(() => new OpenAiAuthenticationHandler(options));

        builder.AddCisharpaiResilienceHandler();

        services.AddKeyedTransient<OpenAiChatCompletionClient>(key, (sp, _) =>
            new OpenAiChatCompletionClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(clientName),
                options));

        services.AddKeyedSingleton<IChatCompletionClient>(key, (sp, k) =>
            sp.GetRequiredKeyedService<OpenAiChatCompletionClient>(k));

        return builder;
    }

    public static IHttpClientBuilder AddOpenAiEmbeddingClient(
        this IServiceCollection services,
        string key,
        Action<OpenAiClientOptions> configure)
    {
        var options = new OpenAiClientOptions();
        configure(options);

        var clientName = $"{nameof(OpenAiEmbeddingClient)}_{key}";

        var builder = services.AddHttpClient(clientName, client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler(() => new OpenAiAuthenticationHandler(options));

        builder.AddCisharpaiResilienceHandler();

        services.AddKeyedTransient<OpenAiEmbeddingClient>(key, (sp, _) =>
            new OpenAiEmbeddingClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(clientName),
                options));

        services.AddKeyedSingleton<IEmbeddingClient>(key, (sp, k) =>
            sp.GetRequiredKeyedService<OpenAiEmbeddingClient>(k));

        return builder;
    }
}
