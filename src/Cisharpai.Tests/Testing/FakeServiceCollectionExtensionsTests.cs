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

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Is.EqualTo("DI works"));
            Assert.That(fake.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void AddFakeChatCompletionClient_SupportsFeatureDiscovery()
    {
        var services = new ServiceCollection();
        services.AddFakeChatCompletionClient();

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        Assert.Multiple(() =>
        {
            Assert.That(client.Features.Get<IStreamingChatFeature>(), Is.Not.Null);
            Assert.That(client.Features.Get<IToolCallingFeature>(), Is.Not.Null);
        });
    }

    [Test]
    public void AddFakeChatCompletionClient_WithSelectiveFeatures()
    {
        var services = new ServiceCollection();
        services.AddFakeChatCompletionClient(FakeChatFeatures.Streaming);

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        Assert.Multiple(() =>
        {
            Assert.That(client.Features.Get<IStreamingChatFeature>(), Is.Not.Null);
            Assert.That(client.Features.Get<IToolCallingFeature>(), Is.Null);
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(fake.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void AddFakeEmbeddingClient_SupportsFeatureDiscovery()
    {
        var services = new ServiceCollection();
        services.AddFakeEmbeddingClient();

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        Assert.Multiple(() =>
        {
            Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Not.Null);
            Assert.That(client.Features.Get<IMultimodalEmbeddingFeature>(), Is.Not.Null);
        });
    }

    [Test]
    public void AddFakeEmbeddingClient_WithSelectiveFeatures()
    {
        var services = new ServiceCollection();
        services.AddFakeEmbeddingClient(FakeEmbeddingFeatures.ImageEmbedding);

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEmbeddingClient>();

        Assert.Multiple(() =>
        {
            Assert.That(client.Features.Get<IImageEmbeddingFeature>(), Is.Not.Null);
            Assert.That(client.Features.Get<IMultimodalEmbeddingFeature>(), Is.Null);
        });
    }

    [Test]
    public async Task AddFakeRerankerClient_RegistersAndReturnsInstance()
    {
        var services = new ServiceCollection();
        var fake = services.AddFakeRerankerClient();
        fake.DefaultResponse = new Cisharpai.Models.RerankResponse(
            Results: [new Cisharpai.Models.RerankResult(0, 0.9)],
            Model: "test-model");

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IRerankerClient>();

        var result = await client.RerankAsync(
            new Cisharpai.Models.RerankRequest("test query", ["doc1"]));

        Assert.Multiple(() =>
        {
            Assert.That(result.Results, Has.Count.EqualTo(1));
            Assert.That(fake.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AddFakeTokenCounter_RegistersAndReturnsInstance()
    {
        var services = new ServiceCollection();
        var fake = services.AddFakeTokenCounter(defaultCount: 42);

        var provider = services.BuildServiceProvider();
        var counter = provider.GetRequiredService<ITokenCounter>();

        var count = await counter.CountAsync("hello");

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(42));
            Assert.That(fake.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AddFakeTokenCounter_WithoutDefault_UsesQueue()
    {
        var services = new ServiceCollection();
        var fake = services.AddFakeTokenCounter();
        fake.EnqueueCount(10);
        fake.EnqueueCount(20);

        var provider = services.BuildServiceProvider();
        var counter = provider.GetRequiredService<ITokenCounter>();

        var first = await counter.CountAsync("a");
        var second = await counter.CountAsync("b");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(10));
            Assert.That(second, Is.EqualTo(20));
            Assert.That(fake.ReceivedTexts, Has.Count.EqualTo(2));
        });
    }
}
