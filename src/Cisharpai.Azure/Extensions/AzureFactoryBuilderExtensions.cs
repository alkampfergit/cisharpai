using System.Linq;
using Cisharpai.Azure.AzureAiInference;
using Cisharpai.Azure.AzureOpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Azure;

public static class AzureFactoryBuilderExtensions
{
    public static ICisharpaiClientFactoryBuilder AddAzureOpenAiSupport(
        this ICisharpaiClientFactoryBuilder builder)
    {
        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(IClientFactoryProvider) &&
                descriptor.ImplementationInstance is AzureOpenAiClientFactoryProvider))
        {
            return builder;
        }

        builder.Services.AddHttpClient(AzureOpenAiClientFactoryProvider.ChatHttpClientName)
            .AddCisharpaiResilienceHandler();

        builder.Services.AddHttpClient(AzureOpenAiClientFactoryProvider.EmbeddingHttpClientName)
            .AddCisharpaiResilienceHandler();

        return builder.AddProvider(new AzureOpenAiClientFactoryProvider());
    }

    public static ICisharpaiClientFactoryBuilder AddAzureAiInferenceSupport(
        this ICisharpaiClientFactoryBuilder builder)
    {
        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(IClientFactoryProvider) &&
                descriptor.ImplementationInstance is AzureAiInferenceClientFactoryProvider))
        {
            return builder;
        }

        builder.Services.AddHttpClient(AzureAiInferenceClientFactoryProvider.ChatHttpClientName)
            .AddCisharpaiResilienceHandler();

        builder.Services.AddHttpClient(AzureAiInferenceClientFactoryProvider.EmbeddingHttpClientName)
            .AddCisharpaiResilienceHandler();

        return builder.AddProvider(new AzureAiInferenceClientFactoryProvider());
    }
}
