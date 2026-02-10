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
}
