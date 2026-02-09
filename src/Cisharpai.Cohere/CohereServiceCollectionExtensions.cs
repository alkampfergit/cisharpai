using Cisharpai;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Cohere;

public static class CohereServiceCollectionExtensions
{
    public static IHttpClientBuilder AddCohereEmbeddingClient(
        this IServiceCollection services,
        Action<CohereClientOptions> configure)
    {
        var options = new CohereClientOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddTransient<CohereAuthenticationHandler>();

        var builder = services.AddHttpClient<CohereEmbeddingClient>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler<CohereAuthenticationHandler>();

        builder.AddCisharpaiResilienceHandler();

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

        services.AddSingleton(options);
        services.AddTransient<CohereAuthenticationHandler>();

        var builder = services.AddHttpClient<CohereChatCompletionClient>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler<CohereAuthenticationHandler>();

        builder.AddCisharpaiResilienceHandler();

        services.AddSingleton<IChatCompletionClient>(sp =>
            sp.GetRequiredService<CohereChatCompletionClient>());

        return builder;
    }
}
