using Cisharpai.Models;
using Cisharpai.Cohere;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.Cohere;

public sealed class CohereChatCompletionIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    [TestCase("command-a-03-2025")]
    [TestCase("command-r-plus-08-2024")]
    public async Task GetChatCompletionAsync_ReturnsValidResponse(string model)
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.CohereTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.CohereTestApiKey} must be set. " +
            "Add it to a .env file in any parent directory.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCohereChatClient(options =>
        {
            options.ApiKey = apiKey!;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Reply with exactly: hello followed by an haiku on something")],
            Model: model,
            Temperature: 0,
            MaxTokens: 1024,
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Content.ToLowerInvariant(), Does.Contain("hello"));
        Assert.That(response.Model, Is.Not.Null.And.Not.Empty);
        Assert.That(response.PromptTokens, Is.GreaterThan(0));
        Assert.That(response.CompletionTokens, Is.GreaterThan(0));
        Assert.That(response.ErrorMessage, Is.Null);
    }

    [TestCase("command-r-plus-08-2024")]
    public async Task GetChatCompletionAsync_VeryLowMaxTokens_ReturnsIncompleteResponse(string model)
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.CohereTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.CohereTestApiKey} must be set. " +
            "Add it to a .env file in any parent directory.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCohereChatClient(options =>
        {
            options.ApiKey = apiKey!;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Write a very long and detailed essay about the history of computing")],
            Model: model,
            Temperature: 0,
            MaxTokens: 16,
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.PromptTokens, Is.GreaterThan(0));
        Assert.That(response.CompletionTokens, Is.GreaterThan(0));
    }
}
