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

        services.AddSingleton(options);
        services.AddTransient<OpenAiAuthenticationHandler>();

        var builder = services.AddHttpClient<OpenAiChatCompletionClient>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddHttpMessageHandler<OpenAiAuthenticationHandler>();

        builder.AddCisharpaiResilienceHandler();

        services.AddSingleton<IChatCompletionClient>(sp =>
            sp.GetRequiredService<OpenAiChatCompletionClient>());

        return builder;
    }
}
