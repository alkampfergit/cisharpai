using Cisharpai.OpenAi;
using Cisharpai.Rag;
using Cisharpai.Rag.OpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.DependencyInjection;

public sealed class OpenAiRagDiRegistrationTests
{
    [Test]
    public void AddOpenAiHostedRetrieval_AfterAddOpenAiClient_FeatureIsDiscoverable()
    {
        var services = new ServiceCollection();

        services.AddOpenAiClient(opt =>
        {
            opt.ApiKey = "test-key";
            opt.DefaultModel = "gpt-4o";
        });

        services.AddOpenAiHostedRetrieval(opt =>
        {
            opt.ApiKey = "test-key";
            opt.DefaultModel = "gpt-4o";
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<OpenAiChatCompletionClient>();
        var feature = client.Features.Get<IHostedRetrievalFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    [Test]
    public void AddOpenAiHostedRetrieval_ForStore_ReturnsRetriever()
    {
        var services = new ServiceCollection();

        services.AddOpenAiClient(opt =>
        {
            opt.ApiKey = "test-key";
            opt.DefaultModel = "gpt-4o";
        });

        services.AddOpenAiHostedRetrieval(opt =>
        {
            opt.ApiKey = "test-key";
            opt.DefaultModel = "gpt-4o";
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<OpenAiChatCompletionClient>();
        var feature = client.Features.Get<IHostedRetrievalFeature>()!;
        var retriever = feature.ForStore("vs_test_store");

        Assert.That(retriever, Is.Not.Null);
        Assert.That(retriever, Is.InstanceOf<IRetriever>());
    }

    [Test]
    public void AddOpenAiHostedRetrieval_RegistersHostedRetrievalFeature_AsSingleton()
    {
        var services = new ServiceCollection();

        services.AddOpenAiClient(opt =>
        {
            opt.ApiKey = "test-key";
            opt.DefaultModel = "gpt-4o";
        });

        services.AddOpenAiHostedRetrieval(opt =>
        {
            opt.ApiKey = "test-key";
            opt.DefaultModel = "gpt-4o";
        });

        using var provider = services.BuildServiceProvider();

        var feature = provider.GetRequiredService<IHostedRetrievalFeature>();

        Assert.That(feature, Is.Not.Null);
        Assert.That(feature, Is.InstanceOf<OpenAiHostedRetrievalFeature>());
    }

    [Test]
    public void AddOpenAiHostedRetrieval_WithoutPriorOpenAiClient_StillRegistersFeature()
    {
        var services = new ServiceCollection();

        services.AddOpenAiHostedRetrieval(opt =>
        {
            opt.ApiKey = "test-key";
            opt.DefaultModel = "gpt-4o";
        });

        using var provider = services.BuildServiceProvider();

        var feature = provider.GetRequiredService<IHostedRetrievalFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    [Test]
    public void AddOpenAiHostedRetrieval_ChatClientAlsoResolvesViaInterface()
    {
        var services = new ServiceCollection();

        services.AddOpenAiClient(opt =>
        {
            opt.ApiKey = "test-key";
            opt.DefaultModel = "gpt-4o";
        });

        services.AddOpenAiHostedRetrieval(opt =>
        {
            opt.ApiKey = "test-key";
            opt.DefaultModel = "gpt-4o";
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IChatCompletionClient>();

        Assert.That(client, Is.InstanceOf<OpenAiChatCompletionClient>());
    }

    [Test]
    public void AddOpenAiHostedRetrieval_FeatureAndDirectResolution_ReturnSameInstance()
    {
        var services = new ServiceCollection();

        services.AddOpenAiClient(opt =>
        {
            opt.ApiKey = "test-key";
            opt.DefaultModel = "gpt-4o";
        });

        services.AddOpenAiHostedRetrieval(opt =>
        {
            opt.ApiKey = "test-key";
            opt.DefaultModel = "gpt-4o";
        });

        using var provider = services.BuildServiceProvider();

        var directFeature = provider.GetRequiredService<IHostedRetrievalFeature>();
        var client = provider.GetRequiredService<OpenAiChatCompletionClient>();
        var discoveredFeature = client.Features.Get<IHostedRetrievalFeature>();

        Assert.That(discoveredFeature, Is.SameAs(directFeature));
    }
}
