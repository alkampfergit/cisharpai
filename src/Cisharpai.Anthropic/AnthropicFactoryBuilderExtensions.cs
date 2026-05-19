using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Anthropic;

public static class AnthropicFactoryBuilderExtensions
{
    public static ICisharpaiClientFactoryBuilder AddAnthropicSupport(
        this ICisharpaiClientFactoryBuilder builder)
    {
        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(IClientFactoryProvider) &&
                descriptor.ImplementationInstance is AnthropicClientFactoryProvider))
        {
            return builder;
        }

        builder.Services.AddHttpClient(AnthropicClientFactoryProvider.ChatHttpClientName)
            .AddCisharpaiResilienceHandler();

        return builder.AddProvider(new AnthropicClientFactoryProvider());
    }
}
