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

        Assert.That(embeddingClient, Is.Not.Null);
        Assert.That(chatClient, Is.Not.Null);
        Assert.That(embeddingClient, Is.Not.SameAs(chatClient));
        Assert.That(embeddingClient, Is.InstanceOf<CohereEmbeddingClient>());
        Assert.That(chatClient, Is.InstanceOf<CohereChatCompletionClient>());
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

        Assert.That(client, Is.Not.Null);
        Assert.That(client, Is.InstanceOf<CohereEmbeddingClient>());
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

        Assert.That(client, Is.Not.Null);
        Assert.That(client, Is.InstanceOf<CohereChatCompletionClient>());
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

        Assert.That(english, Is.Not.Null);
        Assert.That(multilingual, Is.Not.Null);
        Assert.That(english, Is.Not.SameAs(multilingual));
        Assert.That(english, Is.InstanceOf<CohereEmbeddingClient>());
        Assert.That(multilingual, Is.InstanceOf<CohereEmbeddingClient>());
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

        Assert.That(fast, Is.Not.Null);
        Assert.That(quality, Is.Not.Null);
        Assert.That(fast, Is.Not.SameAs(quality));
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

        Assert.That(defaultClient, Is.Not.Null);
        Assert.That(specialClient, Is.Not.Null);
        Assert.That(defaultClient, Is.Not.SameAs(specialClient));
    }
}
