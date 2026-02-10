using Cisharpai.Azure;
using Cisharpai.Azure.AzureAiInference;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.DependencyInjection;

public sealed class AzureAiInferenceDiRegistrationTests
{
    [Test]
    public void BothClients_WithDifferentOptions_ResolveCorrectly()
    {
        var services = new ServiceCollection();

        services.AddAzureAiInferenceChatCompletion(opt =>
        {
            opt.Endpoint = "https://test-chat.inference.azure.com/";
            opt.ApiKey = "chat-key";
            opt.ModelId = "phi-3";
        });

        services.AddAzureAiInferenceEmbeddings(opt =>
        {
            opt.Endpoint = "https://test-embed.inference.azure.com/";
            opt.ApiKey = "embed-key";
            opt.ModelId = "cohere-embed-v3";
        });

        using var provider = services.BuildServiceProvider();

        var chatClient = provider.GetRequiredService<IChatCompletionClient>();
        var embeddingClient = provider.GetRequiredService<IEmbeddingClient>();

        Assert.That(chatClient, Is.Not.Null);
        Assert.That(embeddingClient, Is.Not.Null);
        Assert.That(chatClient, Is.Not.SameAs(embeddingClient));
        Assert.That(chatClient, Is.InstanceOf<AzureAiInferenceChatCompletionClient>());
        Assert.That(embeddingClient, Is.InstanceOf<AzureAiInferenceEmbeddingClient>());
    }

    [Test]
    public void SingleChatClient_ResolvesCorrectly()
    {
        var services = new ServiceCollection();

        services.AddAzureAiInferenceChatCompletion(opt =>
        {
            opt.Endpoint = "https://test.inference.azure.com/";
            opt.ApiKey = "test-key";
            opt.ModelId = "phi-3";
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IChatCompletionClient>();

        Assert.That(client, Is.Not.Null);
        Assert.That(client, Is.InstanceOf<AzureAiInferenceChatCompletionClient>());
    }
}
