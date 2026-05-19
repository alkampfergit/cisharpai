using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Cohere;

public static class CohereFactoryBuilderExtensions
{
    public static ICisharpaiClientFactoryBuilder AddCohereSupport(
        this ICisharpaiClientFactoryBuilder builder)
    {
        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(IClientFactoryProvider) &&
                descriptor.ImplementationInstance is CohereClientFactoryProvider))
        {
            return builder;
        }

        builder.Services.AddHttpClient(CohereClientFactoryProvider.ChatHttpClientName)
            .AddCisharpaiResilienceHandler();

        builder.Services.AddHttpClient(CohereClientFactoryProvider.EmbeddingHttpClientName)
            .AddCisharpaiResilienceHandler();

        return builder.AddProvider(new CohereClientFactoryProvider());
    }
}
