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
}
