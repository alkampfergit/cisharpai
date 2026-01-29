using Cisharpai;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Anthropic;

public static class AnthropicServiceCollectionExtensions
{
    public static IHttpClientBuilder AddAnthropicClient(
        this IServiceCollection services,
        Action<AnthropicClientOptions> configure)
    {
        var options = new AnthropicClientOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddTransient<AnthropicAuthenticationHandler>();

        var builder = services.AddHttpClient<AnthropicChatCompletionClient>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            })
            .AddHttpMessageHandler<AnthropicAuthenticationHandler>();

        builder.AddCisharpaiResilienceHandler();

        services.AddSingleton<IChatCompletionClient>(sp =>
            sp.GetRequiredService<AnthropicChatCompletionClient>());

        return builder;
    }
}
