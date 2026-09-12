using Cisharpai.Anthropic;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.Factory;

public sealed class CisharpaiClientFactoryTests
{
    [Test]
    public void Factory_ResolvesFromDi()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddOpenAiSupport();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

        Assert.That(factory, Is.Not.Null);
    }

    [Test]
    public void GetRegisteredProviders_ReturnsRegistered()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddOpenAiSupport()
            .AddAnthropicSupport();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

        var registered = factory.GetRegisteredProviders();

        Assert.That(registered, Has.Count.EqualTo(2));
        Assert.That(registered, Does.Contain(CisharpaiProvider.OpenAi));
        Assert.That(registered, Does.Contain(CisharpaiProvider.Anthropic));
    }

    [Test]
    public void GetRegisteredProviders_EmptyWhenNoneRegistered()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

        Assert.That(factory.GetRegisteredProviders(), Is.Empty);
    }

    [Test]
    public void CreateChatClient_UnregisteredProvider_ReturnsFailure()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddOpenAiSupport();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

        var config = new AnthropicClientConfiguration { ApiKey = "test" };
        var result = factory.CreateChatCompletionClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Anthropic"));
            Assert.That(result.ErrorMessage, Does.Contain("not registered"));
        });
    }

    [Test]
    public void CreateEmbeddingClient_UnsupportedProvider_ReturnsFailure()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddAnthropicSupport();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

        var config = new AnthropicClientConfiguration { ApiKey = "test" };
        var result = factory.CreateEmbeddingClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("does not support embedding"));
        });
    }

    [Test]
    public void CreateChatClient_WrongConfigType_ReturnsFailure()
    {
        var openAiProvider = new OpenAiClientFactoryProvider();
        var wrongConfig = new AnthropicClientConfiguration { ApiKey = "test" };

        var result = openAiProvider.CreateChatCompletionClient(null!, wrongConfig);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Expected OpenAiClientConfiguration"));
            Assert.That(result.ErrorMessage, Does.Contain("AnthropicClientConfiguration"));
        });
    }

    [Test]
    public void CreateEmbeddingClient_WrongConfigType_ReturnsFailure()
    {
        var openAiProvider = new OpenAiClientFactoryProvider();
        var wrongConfig = new AnthropicClientConfiguration { ApiKey = "test" };

        var result = openAiProvider.CreateEmbeddingClient(null!, wrongConfig);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Expected OpenAiClientConfiguration"));
            Assert.That(result.ErrorMessage, Does.Contain("AnthropicClientConfiguration"));
        });
    }

    [Test]
    public void Factory_IsThreadSafe_ConcurrentCalls()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddOpenAiSupport();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

        var tasks = Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() =>
            {
                var config = new OpenAiClientConfiguration { ApiKey = "test-key" };
                return factory.CreateChatCompletionClient(config);
            })).ToArray();

        Task.WaitAll(tasks);

        Assert.That(tasks.All(t => t.Result.IsSuccess), Is.True);
    }

    [Test]
    public void AddProvider_Idempotent_NoDuplicates()
    {
        var services = new ServiceCollection();
        var builder = services.AddCisharpaiClientFactory();
        builder.AddOpenAiSupport();
        var descriptorCountAfterFirstRegistration = services.Count;
        builder.AddOpenAiSupport();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

        Assert.That(services.Count, Is.EqualTo(descriptorCountAfterFirstRegistration));
        Assert.That(factory.GetRegisteredProviders(), Has.Count.EqualTo(1));
    }

    [Test]
    public void CreateRerankerClient_UnregisteredProvider_ReturnsFailure()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddOpenAiSupport();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

        var config = new AnthropicClientConfiguration { ApiKey = "test" };
        var result = factory.CreateRerankerClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Anthropic"));
            Assert.That(result.ErrorMessage, Does.Contain("not registered"));
        });
    }

    [Test]
    public void CreateRerankerClient_UnsupportedProvider_ReturnsFailure()
    {
        var services = new ServiceCollection();
        services.AddCisharpaiClientFactory()
            .AddOpenAiSupport();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

        var config = new OpenAiClientConfiguration { ApiKey = "test" };
        var result = factory.CreateRerankerClient(config);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("does not support reranker"));
        });
    }

    [Test]
    public void SupportsReranking_DefaultsToFalseForProvidersWithoutRerankSupport()
    {
        Assert.Multiple(() =>
        {
            Assert.That(((IClientFactoryProvider)new OpenAiClientFactoryProvider()).SupportsReranking, Is.False);
            Assert.That(((IClientFactoryProvider)new AnthropicClientFactoryProvider()).SupportsReranking, Is.False);
        });
    }
}
