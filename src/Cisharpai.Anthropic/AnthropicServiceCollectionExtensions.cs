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

        var builder = services.AddHttpClient<AnthropicChatCompletionClient>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            })
            .AddHttpMessageHandler(() => new AnthropicAuthenticationHandler(options));

        builder.AddCisharpaiResilienceHandler();

        services.AddTransient(sp =>
            new AnthropicChatCompletionClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(AnthropicChatCompletionClient).Name),
                options));

        services.AddSingleton<IChatCompletionClient>(sp =>
            sp.GetRequiredService<AnthropicChatCompletionClient>());

        return builder;
    }
}
