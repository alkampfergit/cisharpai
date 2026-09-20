using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.DependencyInjection;

public sealed class OpenAiVectorStoreDiRegistrationTests
{
    [Test]
    public void AddOpenAiVectorStoreClient_ResolvesCorrectly()
    {
        var services = new ServiceCollection();

        services.AddOpenAiVectorStoreClient(opt =>
        {
            opt.ApiKey = "test-key";
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<OpenAiVectorStoreClient>();

        Assert.That(client, Is.Not.Null);
    }

    [Test]
    public void AddOpenAiVectorStoreClient_CoexistsWithChatClient()
    {
        var services = new ServiceCollection();

        services.AddOpenAiClient(opt =>
        {
            opt.ApiKey = "chat-key";
        });

        services.AddOpenAiVectorStoreClient(opt =>
        {
            opt.ApiKey = "vs-key";
        });

        using var provider = services.BuildServiceProvider();

        var chatClient = provider.GetRequiredService<IChatCompletionClient>();
        var vectorStoreClient = provider.GetRequiredService<OpenAiVectorStoreClient>();

        Assert.Multiple(() =>
        {
            Assert.That(chatClient, Is.Not.Null);
            Assert.That(vectorStoreClient, Is.Not.Null);
            Assert.That(chatClient, Is.InstanceOf<OpenAiChatCompletionClient>());
        });
    }

    [Test]
    public void AddOpenAiVectorStoreClient_CoexistsWithEmbeddingClient()
    {
        var services = new ServiceCollection();

        services.AddOpenAiEmbeddingClient(opt =>
        {
            opt.ApiKey = "embed-key";
        });

        services.AddOpenAiVectorStoreClient(opt =>
        {
            opt.ApiKey = "vs-key";
        });

        using var provider = services.BuildServiceProvider();

        var embeddingClient = provider.GetRequiredService<IEmbeddingClient>();
        var vectorStoreClient = provider.GetRequiredService<OpenAiVectorStoreClient>();

        Assert.Multiple(() =>
        {
            Assert.That(embeddingClient, Is.Not.Null);
            Assert.That(vectorStoreClient, Is.Not.Null);
        });
    }

    [Test]
    public void KeyedEmbeddingClients_ResolveIndependentlyByKey()
    {
        var services = new ServiceCollection();

        services.AddOpenAiEmbeddingClient("fast", opt =>
        {
            opt.ApiKey = "key";
            opt.DefaultModel = "text-embedding-3-small";
        });

        services.AddOpenAiEmbeddingClient("quality", opt =>
        {
            opt.ApiKey = "key";
            opt.DefaultModel = "text-embedding-3-large";
        });

        using var provider = services.BuildServiceProvider();

        var fast = provider.GetRequiredKeyedService<IEmbeddingClient>("fast");
        var quality = provider.GetRequiredKeyedService<IEmbeddingClient>("quality");

        Assert.Multiple(() =>
        {
            Assert.That(fast, Is.Not.Null);
            Assert.That(quality, Is.Not.Null);
            Assert.That(fast, Is.Not.SameAs(quality));
            Assert.That(fast, Is.InstanceOf<OpenAiEmbeddingClient>());
        });
    }
}
