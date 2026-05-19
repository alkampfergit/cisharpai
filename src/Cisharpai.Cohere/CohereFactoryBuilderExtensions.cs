using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Cohere;

public static class CohereFactoryBuilderExtensions
{
    public static ICisharpaiClientFactoryBuilder AddCohereSupport(
        this ICisharpaiClientFactoryBuilder builder)
    {
        if (builder.IsProviderRegistered(CisharpaiProvider.Cohere))
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
