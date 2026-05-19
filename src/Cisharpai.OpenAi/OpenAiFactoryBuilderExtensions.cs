using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.OpenAi;

public static class OpenAiFactoryBuilderExtensions
{
    public static ICisharpaiClientFactoryBuilder AddOpenAiSupport(
        this ICisharpaiClientFactoryBuilder builder)
    {
        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(IClientFactoryProvider) &&
                descriptor.ImplementationInstance is OpenAiClientFactoryProvider))
        {
            return builder;
        }

        builder.Services.AddHttpClient(OpenAiClientFactoryProvider.ChatHttpClientName)
            .AddCisharpaiResilienceHandler();

        builder.Services.AddHttpClient(OpenAiClientFactoryProvider.EmbeddingHttpClientName)
            .AddCisharpaiResilienceHandler();

        return builder.AddProvider(new OpenAiClientFactoryProvider());
    }
}
