using Cisharpai.OpenAi;
using Cisharpai.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.Factory;

public sealed class FakeClientFactoryTests
{
    [Test]
    public void FakeProvider_ReturnsFakeChatClient()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddFakeSupport();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

        var config = new OpenAiClientConfiguration { ApiKey = "fake" };
        var result = factory.CreateChatCompletionClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.InstanceOf<FakeChatCompletionClient>());
        });
    }

    [Test]
    public void FakeProvider_ReturnsFakeEmbeddingClient()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddFakeSupport();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

        var config = new OpenAiClientConfiguration { ApiKey = "fake" };
        var result = factory.CreateEmbeddingClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.InstanceOf<FakeEmbeddingClient>());
        });
    }

    [Test]
    public void FakeProvider_DequeuesInOrder()
    {
        var client1 = new FakeChatCompletionClient();
        var client2 = new FakeChatCompletionClient();

        var fakeProvider = new FakeClientFactoryProvider()
            .EnqueueChatClient(client1)
            .EnqueueChatClient(client2);

        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddFakeSupport(fakeProvider);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();
        var config = new OpenAiClientConfiguration { ApiKey = "fake" };

        var result1 = factory.CreateChatCompletionClient(config);
        var result2 = factory.CreateChatCompletionClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result1.Client, Is.SameAs(client1));
            Assert.That(result2.Client, Is.SameAs(client2));
        });
    }

    [Test]
    public void FakeProvider_UsesDefaultWhenQueueEmpty()
    {
        var defaultClient = new FakeChatCompletionClient();
        var fakeProvider = new FakeClientFactoryProvider()
            .WithDefaultChatClient(defaultClient);

        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddFakeSupport(fakeProvider);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();
        var config = new OpenAiClientConfiguration { ApiKey = "fake" };

        var result = factory.CreateChatCompletionClient(config);

        Assert.That(result.Client, Is.SameAs(defaultClient));
    }
}
