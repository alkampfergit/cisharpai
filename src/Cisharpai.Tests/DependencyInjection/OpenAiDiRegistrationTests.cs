using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.DependencyInjection;

public sealed class OpenAiDiRegistrationTests
{
    [Test]
    public void BothClients_WithDifferentOptions_ResolveCorrectly()
    {
        var services = new ServiceCollection();

        services.AddOpenAiClient(opt =>
        {
            opt.ApiKey = "chat-key";
        });

        services.AddOpenAiEmbeddingClient(opt =>
        {
            opt.ApiKey = "embed-key";
        });

        using var provider = services.BuildServiceProvider();

        var chatClient = provider.GetRequiredService<IChatCompletionClient>();
        var embeddingClient = provider.GetRequiredService<IEmbeddingClient>();

        Assert.That(chatClient, Is.Not.Null);
        Assert.That(embeddingClient, Is.Not.Null);
        Assert.That(chatClient, Is.Not.SameAs(embeddingClient));
        Assert.That(chatClient, Is.InstanceOf<OpenAiChatCompletionClient>());
        Assert.That(embeddingClient, Is.InstanceOf<OpenAiEmbeddingClient>());
    }

    [Test]
    public void SingleChatClient_ResolvesCorrectly()
    {
        var services = new ServiceCollection();

        services.AddOpenAiClient(opt =>
        {
            opt.ApiKey = "test-key";
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IChatCompletionClient>();

        Assert.That(client, Is.Not.Null);
        Assert.That(client, Is.InstanceOf<OpenAiChatCompletionClient>());
    }
}
