using Cisharpai.Cohere;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.DependencyInjection;

public sealed class CohereDiRegistrationTests
{
    [Test]
    public void BothClients_WithDifferentOptions_ResolveCorrectly()
    {
        var services = new ServiceCollection();

        services.AddCohereEmbeddingClient(opt =>
        {
            opt.ApiKey = "embed-key";
            opt.DefaultModel = "embed-english-v3.0";
        });

        services.AddCohereChatClient(opt =>
        {
            opt.ApiKey = "chat-key";
            opt.DefaultModel = "command-a-03-2025";
        });

        using var provider = services.BuildServiceProvider();

        var embeddingClient = provider.GetRequiredService<IEmbeddingClient>();
        var chatClient = provider.GetRequiredService<IChatCompletionClient>();

        Assert.Multiple(() =>
        {
            Assert.That(embeddingClient, Is.Not.Null);
            Assert.That(chatClient, Is.Not.Null);
            Assert.That(embeddingClient, Is.Not.SameAs(chatClient));
            Assert.That(embeddingClient, Is.InstanceOf<CohereEmbeddingClient>());
            Assert.That(chatClient, Is.InstanceOf<CohereChatCompletionClient>());
        });
    }

    [Test]
    public void SingleEmbeddingClient_ResolvesCorrectly()
    {
        var services = new ServiceCollection();

        services.AddCohereEmbeddingClient(opt =>
        {
            opt.ApiKey = "test-key";
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IEmbeddingClient>();

        Assert.Multiple(() =>
        {
            Assert.That(client, Is.Not.Null);
            Assert.That(client, Is.InstanceOf<CohereEmbeddingClient>());
        });
    }

    [Test]
    public void SingleChatClient_ResolvesCorrectly()
    {
        var services = new ServiceCollection();

        services.AddCohereChatClient(opt =>
        {
            opt.ApiKey = "test-key";
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IChatCompletionClient>();

        Assert.Multiple(() =>
        {
            Assert.That(client, Is.Not.Null);
            Assert.That(client, Is.InstanceOf<CohereChatCompletionClient>());
        });
    }

    [Test]
    public void KeyedEmbeddingClients_ResolveIndependentlyByKey()
    {
        var services = new ServiceCollection();

        services.AddCohereEmbeddingClient("english", opt =>
        {
            opt.ApiKey = "key";
            opt.DefaultModel = "embed-english-v3.0";
        });

        services.AddCohereEmbeddingClient("multilingual", opt =>
        {
            opt.ApiKey = "key";
            opt.DefaultModel = "embed-multilingual-v3.0";
        });

        using var provider = services.BuildServiceProvider();

        var english = provider.GetRequiredKeyedService<IEmbeddingClient>("english");
        var multilingual = provider.GetRequiredKeyedService<IEmbeddingClient>("multilingual");

        Assert.Multiple(() =>
        {
            Assert.That(english, Is.Not.Null);
            Assert.That(multilingual, Is.Not.Null);
            Assert.That(english, Is.Not.SameAs(multilingual));
            Assert.That(english, Is.InstanceOf<CohereEmbeddingClient>());
            Assert.That(multilingual, Is.InstanceOf<CohereEmbeddingClient>());
        });
    }

    [Test]
    public void KeyedChatClients_ResolveIndependentlyByKey()
    {
        var services = new ServiceCollection();

        services.AddCohereChatClient("fast", opt =>
        {
            opt.ApiKey = "key";
            opt.DefaultModel = "command-r";
        });

        services.AddCohereChatClient("quality", opt =>
        {
            opt.ApiKey = "key";
            opt.DefaultModel = "command-a-03-2025";
        });

        using var provider = services.BuildServiceProvider();

        var fast = provider.GetRequiredKeyedService<IChatCompletionClient>("fast");
        var quality = provider.GetRequiredKeyedService<IChatCompletionClient>("quality");

        Assert.Multiple(() =>
        {
            Assert.That(fast, Is.Not.Null);
            Assert.That(quality, Is.Not.Null);
            Assert.That(fast, Is.Not.SameAs(quality));
        });
    }

    [Test]
    public void KeyedAndNonKeyed_CoexistIndependently()
    {
        var services = new ServiceCollection();

        services.AddCohereEmbeddingClient(opt =>
        {
            opt.ApiKey = "default-key";
        });

        services.AddCohereEmbeddingClient("special", opt =>
        {
            opt.ApiKey = "special-key";
        });

        using var provider = services.BuildServiceProvider();

        var defaultClient = provider.GetRequiredService<IEmbeddingClient>();
        var specialClient = provider.GetRequiredKeyedService<IEmbeddingClient>("special");

        Assert.Multiple(() =>
        {
            Assert.That(defaultClient, Is.Not.Null);
            Assert.That(specialClient, Is.Not.Null);
            Assert.That(defaultClient, Is.Not.SameAs(specialClient));
        });
    }

    [Test]
    public void SingleRerankerClient_ResolvesCorrectly()
    {
        var services = new ServiceCollection();

        services.AddCohereRerankerClient(opt =>
        {
            opt.ApiKey = "test-key";
            opt.DefaultModel = CohereModels.Rerank.RerankV3_5;
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IRerankerClient>();

        Assert.Multiple(() =>
        {
            Assert.That(client, Is.Not.Null);
            Assert.That(client, Is.InstanceOf<CohereRerankerClient>());
        });
    }

    [Test]
    public void RerankerClient_CoexistsWithChatAndEmbeddingClients()
    {
        var services = new ServiceCollection();

        services.AddCohereEmbeddingClient(opt => opt.ApiKey = "embed-key");
        services.AddCohereChatClient(opt => opt.ApiKey = "chat-key");
        services.AddCohereRerankerClient(opt => opt.ApiKey = "rerank-key");

        using var provider = services.BuildServiceProvider();

        var embeddingClient = provider.GetRequiredService<IEmbeddingClient>();
        var chatClient = provider.GetRequiredService<IChatCompletionClient>();
        var rerankerClient = provider.GetRequiredService<IRerankerClient>();

        Assert.Multiple(() =>
        {
            Assert.That(rerankerClient, Is.InstanceOf<CohereRerankerClient>());
            Assert.That(rerankerClient, Is.Not.SameAs(embeddingClient));
            Assert.That(rerankerClient, Is.Not.SameAs(chatClient));
        });
    }

    [Test]
    public void KeyedRerankerClients_ResolveIndependentlyByKey()
    {
        var services = new ServiceCollection();

        services.AddCohereRerankerClient("tenant-a", opt =>
        {
            opt.ApiKey = "key-a";
            opt.DefaultModel = CohereModels.Rerank.RerankV3_5;
        });

        services.AddCohereRerankerClient("tenant-b", opt =>
        {
            opt.ApiKey = "key-b";
            opt.DefaultModel = CohereModels.Rerank.RerankEnglishV3;
        });

        using var provider = services.BuildServiceProvider();

        var tenantA = provider.GetRequiredKeyedService<IRerankerClient>("tenant-a");
        var tenantB = provider.GetRequiredKeyedService<IRerankerClient>("tenant-b");

        Assert.Multiple(() =>
        {
            Assert.That(tenantA, Is.InstanceOf<CohereRerankerClient>());
            Assert.That(tenantB, Is.InstanceOf<CohereRerankerClient>());
            Assert.That(tenantA, Is.Not.SameAs(tenantB));
        });
    }

    [Test]
    public void KeyedAndNonKeyedRerankerClients_CoexistIndependently()
    {
        var services = new ServiceCollection();

        services.AddCohereRerankerClient(opt => opt.ApiKey = "default-key");
        services.AddCohereRerankerClient("special", opt => opt.ApiKey = "special-key");

        using var provider = services.BuildServiceProvider();

        var defaultClient = provider.GetRequiredService<IRerankerClient>();
        var specialClient = provider.GetRequiredKeyedService<IRerankerClient>("special");

        Assert.That(defaultClient, Is.Not.SameAs(specialClient));
    }
}
