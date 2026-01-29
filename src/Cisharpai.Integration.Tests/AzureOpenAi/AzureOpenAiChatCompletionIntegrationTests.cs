using Cisharpai.Models;
using Cisharpai.AzureOpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.AzureOpenAi;

public sealed class AzureOpenAiChatCompletionIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    private static IEnumerable<string> Deployments()
    {
        DotEnv.Load();
        var raw = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestDeployments);
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        foreach (var deployment in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return deployment;
    }

    [TestCaseSource(nameof(Deployments))]
    public async Task GetChatCompletionAsync_ReturnsValidResponse(string deployment)
    {
        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestApiKey);

        Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestEndpoint} must be set.");
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureOpenAiClient(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = apiKey!;
            options.DeploymentName = deployment;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Reply with exactly: hello followed by an haiku on something")],
            Model: deployment,
            Temperature: 0,
            MaxTokens: 8000, // high number to account for reasoning tokens on gpt-5/o-series models
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

    [TestCaseSource(nameof(Deployments))]
    public async Task GetChatCompletionAsync_VeryLowMaxTokens_ReturnsTruncatedResponse(string deployment)
    {
        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestApiKey);

        Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestEndpoint} must be set.");
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureOpenAiClient(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = apiKey!;
            options.DeploymentName = deployment;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Write a very long and detailed essay about the history of computing")],
            Model: deployment,
            Temperature: 0,
            MaxTokens: 16,
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        // Reasoning models (gpt-5, o-series) may return empty content when
        // max_completion_tokens is very low because all tokens are used for reasoning.
        Assert.That(response.Content, Is.Not.Null);
        Assert.That(response.PromptTokens, Is.GreaterThan(0));
    }
}
