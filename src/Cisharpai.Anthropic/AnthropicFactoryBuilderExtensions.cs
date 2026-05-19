using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Anthropic;

public static class AnthropicFactoryBuilderExtensions
{
    public static ICisharpaiClientFactoryBuilder AddAnthropicSupport(
        this ICisharpaiClientFactoryBuilder builder)
    {
        if (builder.IsProviderRegistered(CisharpaiProvider.Anthropic))
        {
            return builder;
        }

        builder.Services.AddHttpClient(AnthropicClientFactoryProvider.ChatHttpClientName)
            .AddCisharpaiResilienceHandler();

        return builder.AddProvider(new AnthropicClientFactoryProvider());
    }
}
