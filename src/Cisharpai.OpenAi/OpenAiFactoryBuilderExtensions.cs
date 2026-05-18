using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.OpenAi;

public static class OpenAiFactoryBuilderExtensions
{
    public static ICisharpaiClientFactoryBuilder AddOpenAiSupport(
        this ICisharpaiClientFactoryBuilder builder)
    {
        builder.Services.AddHttpClient(OpenAiClientFactoryProvider.ChatHttpClientName)
            .AddCisharpaiResilienceHandler();

        builder.Services.AddHttpClient(OpenAiClientFactoryProvider.EmbeddingHttpClientName)
            .AddCisharpaiResilienceHandler();

        return builder.AddProvider(new OpenAiClientFactoryProvider());
    }
}
