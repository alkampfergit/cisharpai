using Cisharpai.Anthropic;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.DependencyInjection;

public sealed class AnthropicDiRegistrationTests
{
    [Test]
    public void ChatClient_ResolvesCorrectly()
    {
        var services = new ServiceCollection();

        services.AddAnthropicClient(opt =>
        {
            opt.ApiKey = "test-key";
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IChatCompletionClient>();

        Assert.That(client, Is.Not.Null);
        Assert.That(client, Is.InstanceOf<AnthropicChatCompletionClient>());
    }

    [Test]
    public void KeyedChatClient_ResolvesByKey()
    {
        var services = new ServiceCollection();

        services.AddAnthropicClient("claude", opt =>
        {
            opt.ApiKey = "test-key";
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredKeyedService<IChatCompletionClient>("claude");

        Assert.That(client, Is.Not.Null);
        Assert.That(client, Is.InstanceOf<AnthropicChatCompletionClient>());
    }
}
