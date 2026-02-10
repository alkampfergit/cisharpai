using Cisharpai.Azure;
using Cisharpai.Azure.AzureOpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.DependencyInjection;

public sealed class AzureOpenAiDiRegistrationTests
{
    [Test]
    public void BothClients_WithDifferentOptions_ResolveCorrectly()
    {
        var services = new ServiceCollection();

        services.AddAzureOpenAiClient(opt =>
        {
            opt.Endpoint = "https://test-chat.openai.azure.com/";
            opt.ApiKey = "chat-key";
            opt.DeploymentName = "gpt-4";
        });

        services.AddAzureOpenAiEmbeddingClient(opt =>
        {
            opt.Endpoint = "https://test-embed.openai.azure.com/";
            opt.ApiKey = "embed-key";
            opt.DeploymentName = "text-embedding-3-small";
        });

        using var provider = services.BuildServiceProvider();

        var chatClient = provider.GetRequiredService<IChatCompletionClient>();
        var embeddingClient = provider.GetRequiredService<IEmbeddingClient>();

        Assert.That(chatClient, Is.Not.Null);
        Assert.That(embeddingClient, Is.Not.Null);
        Assert.That(chatClient, Is.Not.SameAs(embeddingClient));
        Assert.That(chatClient, Is.InstanceOf<AzureOpenAiChatCompletionClient>());
        Assert.That(embeddingClient, Is.InstanceOf<AzureOpenAiEmbeddingClient>());
    }

    [Test]
    public void SingleChatClient_ResolvesCorrectly()
    {
        var services = new ServiceCollection();

        services.AddAzureOpenAiClient(opt =>
        {
            opt.Endpoint = "https://test.openai.azure.com/";
            opt.ApiKey = "test-key";
            opt.DeploymentName = "gpt-4";
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IChatCompletionClient>();

        Assert.That(client, Is.Not.Null);
        Assert.That(client, Is.InstanceOf<AzureOpenAiChatCompletionClient>());
    }

    [Test]
    public void KeyedClients_ResolveIndependentlyByKey()
    {
        var services = new ServiceCollection();

        services.AddAzureOpenAiClient("primary", opt =>
        {
            opt.Endpoint = "https://primary.openai.azure.com/";
            opt.ApiKey = "primary-key";
            opt.DeploymentName = "gpt-4";
        });

        services.AddAzureOpenAiClient("secondary", opt =>
        {
            opt.Endpoint = "https://secondary.openai.azure.com/";
            opt.ApiKey = "secondary-key";
            opt.DeploymentName = "gpt-4o";
        });

        using var provider = services.BuildServiceProvider();

        var primary = provider.GetRequiredKeyedService<IChatCompletionClient>("primary");
        var secondary = provider.GetRequiredKeyedService<IChatCompletionClient>("secondary");

        Assert.That(primary, Is.Not.Null);
        Assert.That(secondary, Is.Not.Null);
        Assert.That(primary, Is.Not.SameAs(secondary));
        Assert.That(primary, Is.InstanceOf<AzureOpenAiChatCompletionClient>());
    }
}
