using Cisharpai.Features.Chat;
using Cisharpai.Features.Embeddings;
using Cisharpai.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.Testing;

[TestFixture]
public class FakeServiceCollectionExtensionsTests
{
    [Test]
    public async Task AddFakeChatCompletionClient_RegistersAndReturnsInstance()
    {
        var services = new ServiceCollection();
        var fake = services.AddFakeChatCompletionClient();
        fake.DefaultResponse = FakeResponses.Chat("DI works");

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var result = await client.GetChatCompletionAsync(
            new Cisharpai.Models.ChatCompletionRequest(
                [new Cisharpai.Models.LlmMessage(Cisharpai.Models.LlmRole.User, "test")]));

        Assert.That(result.Content, Is.EqualTo("DI works"));
        Assert.That(fake.CallCount, Is.EqualTo(1));
    }

    [Test]
    public void AddFakeChatCompletionClient_SupportsFeatureDiscovery()
    {
        var services = new ServiceCollection();
        services.AddFakeChatCompletionClient();

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        Assert.That(client.Features.Get<IStreamingChatFeature>(), Is.Not.Null);
        Assert.That(client.Features.Get<IToolCallingFeature>(), Is.Not.Null);
    }

    [Test]
    public void AddFakeChatCompletionClient_WithSelectiveFeatures()
    {
        var services = new ServiceCollection();
        services.AddFakeChatCompletionClient(FakeChatFeatures.Streaming);

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        Assert.That(client.Features.Get<IStreamingChatFeature>(), Is.Not.Null);
        Assert.That(client.Features.Get<IToolCallingFeature>(), Is.Null);
    }

    [Test]
    public async Task AddFakeEmbeddingClient_RegistersAndReturnsInstance()
    {
        var services = new ServiceCollection();
        var fake = services.AddFakeEmbeddingClient();
        fake.DefaultResponse = FakeResponses.Embedding();

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        var result = await client.GetEmbeddingsAsync(
            new Cisharpai.Models.EmbeddingRequest(["hello"]));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(fake.CallCount, Is.EqualTo(1));
    }

    [Test]
    public void AddFakeEmbeddingClient_SupportsFeatureDiscovery()
    {
        var services = new ServiceCollection();
        services.AddFakeEmbeddingClient();

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Not.Null);
        Assert.That(client.Features.Get<IMultimodalEmbeddingFeature>(), Is.Not.Null);
    }

    [Test]
    public void AddFakeEmbeddingClient_WithSelectiveFeatures()
    {
        var services = new ServiceCollection();
        services.AddFakeEmbeddingClient(FakeEmbeddingFeatures.ImageEmbedding);

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Not.Null);
        Assert.That(client.Features.Get<IMultimodalEmbeddingFeature>(), Is.Null);
    }
}
