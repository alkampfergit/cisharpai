using Cisharpai.Models;
using Cisharpai.Anthropic;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.Anthropic;

public sealed class AnthropicChatCompletionIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    [TestCase("claude-opus-4-5-20251101")]
    [TestCase("claude-sonnet-4-5-20250929")]
    [TestCase("claude-haiku-4-5-20251001")]
    public async Task GetChatCompletionAsync_ReturnsValidResponse(string model)
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AnthropicTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AnthropicTestApiKey} must be set. " +
            "Add it to a .env file in any parent directory.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnthropicClient(options =>
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

    [TestCase("claude-haiku-4-5-20251001")]
    public async Task GetChatCompletionAsync_WithoutMaxTokens_ReturnsValidResponse(string model)
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AnthropicTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AnthropicTestApiKey} must be set. " +
            "Add it to a .env file in any parent directory.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnthropicClient(options =>
        {
            options.ApiKey = apiKey!;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        // Explicitly NOT setting MaxTokens — should still work
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Say hello")],
            Model: model,
            Temperature: 0);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
    }

    [TestCase("claude-haiku-4-5-20251001")]
    public async Task GetChatCompletionAsync_VeryLowMaxTokens_ReturnsIncompleteResponse(string model)
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AnthropicTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AnthropicTestApiKey} must be set. " +
            "Add it to a .env file in any parent directory.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnthropicClient(options =>
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
        // Anthropic returns a stop_reason of "max_tokens" when output is truncated
        // The content should still be present but truncated
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.PromptTokens, Is.GreaterThan(0));
        Assert.That(response.CompletionTokens, Is.GreaterThan(0));
    }
}
