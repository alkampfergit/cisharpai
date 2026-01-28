using System.Net;
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.OpenAi;

public sealed class OpenAiChatCompletionIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    [Test]
    public async Task GetChatCompletionAsync_ReturnsValidResponse()
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.OpenAiTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.OpenAiTestApiKey} must be set. " +
            "Add it to a .env file in any parent directory.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenAiClient(options =>
        {
            options.ApiKey = apiKey!;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Reply with exactly: hello followed by an haiku on something")],
            Model: "gpt-4.1-nano",
            Temperature: 0,
            MaxTokens: 20);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Content.ToLowerInvariant(), Does.Contain("hello"));
        Assert.That(response.Model, Is.Not.Null.And.Not.Empty);
        Assert.That(response.PromptTokens, Is.GreaterThan(0));
        Assert.That(response.CompletionTokens, Is.GreaterThan(0));
    }
}
