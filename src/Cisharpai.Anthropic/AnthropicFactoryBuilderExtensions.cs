using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Anthropic;

public static class AnthropicFactoryBuilderExtensions
{
    public static ICisharpaiClientFactoryBuilder AddAnthropicSupport(
        this ICisharpaiClientFactoryBuilder builder)
    {
        builder.Services.AddHttpClient(AnthropicClientFactoryProvider.ChatHttpClientName)
            .AddCisharpaiResilienceHandler();

        return builder.AddProvider(new AnthropicClientFactoryProvider());
    }
}
