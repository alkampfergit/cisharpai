using Cisharpai.Models;
using Cisharpai.Azure;
using Cisharpai.Azure.AzureOpenAi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Cisharpai.Integration.Tests.AzureOpenAi;

public sealed class AzureOpenAiChatCompletionIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
        var raw = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestDeployments);
        Assert.That(raw, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureOpenAiTestDeployments} must be set with comma-separated deployment names.");
    }

    private static IEnumerable<string> Deployments()
    {
        DotEnv.Load();
        var raw = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestDeployments);
        if (string.IsNullOrWhiteSpace(raw))
        {
            yield return "__MISSING_DEPLOYMENTS__";
            yield break;
        }

        foreach (var deployment in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return deployment;
    }

    private static bool IsReasoningDeployment(string deployment) =>
        deployment.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
        deployment.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
        deployment.StartsWith("o4", StringComparison.OrdinalIgnoreCase) ||
        deployment.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);

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
    public async Task GetChatCompletionAsync_VeryLowMaxTokens_ReturnsFailureForLengthTruncation(string deployment)
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
            MaxTokens: 1,
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.False, response.RawResponseJson);
        if (response.Status is "length")
        {
            Assert.Multiple(() =>
            {
                Assert.That(response.IncompleteReason, Is.EqualTo("length"), response.RawResponseJson);
                Assert.That(response.ErrorMessage, Does.Contain("finish_reason"));
            });
        }
        else
        {
            Assert.That(response.ErrorMessage, Does.Contain("max_tokens").Or.Contain("output limit"));
        }
    }

    [TestCaseSource(nameof(Deployments))]
    public async Task GetChatCompletionAsync_ViaCreateFactoryMethod_ReturnsValidResponse(string deployment)
    {
        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestApiKey);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
                $"Environment variable {DotEnv.AzureOpenAiTestEndpoint} must be set.");
            Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
                $"Environment variable {DotEnv.AzureOpenAiTestApiKey} must be set.");
        });

        var services = new ServiceCollection();
        services.AddHttpClient();
        await using var provider = services.BuildServiceProvider();
        var handlerFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();

        var client = AzureOpenAiChatCompletionClient.Create(
            handlerFactory,
            new AzureOpenAiClientOptions
            {
                Endpoint = endpoint!,
                ApiKey = apiKey!,
                DeploymentName = deployment
            });

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Reply with exactly: hello")],
            Model: deployment,
            Temperature: 0,
            MaxTokens: 256,
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}\nRaw: {response.RawResponseJson}");
            Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
            Assert.That(response.PromptTokens, Is.GreaterThan(0));
            Assert.That(response.CompletionTokens, Is.GreaterThan(0));
        });
    }

    [TestCaseSource(nameof(Deployments))]
    public async Task GetChatCompletionAsync_ReasoningEffort_WorksForReasoningDeployment(string deployment)
    {
        if (!IsReasoningDeployment(deployment))
            Assert.Ignore($"Deployment {deployment} is not a reasoning model deployment.");

        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestApiKey);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
                $"Environment variable {DotEnv.AzureOpenAiTestEndpoint} must be set.");
            Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
                $"Environment variable {DotEnv.AzureOpenAiTestApiKey} must be set.");
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureOpenAiClient(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = apiKey!;
            options.DeploymentName = deployment;
            options.ReasoningEffort = "low";
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Reply with exactly one word: hello")],
            Model: deployment,
            MaxTokens: 256,
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response.IsSuccess, Is.True, response.RawResponseJson ?? response.ErrorMessage);
            Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
            Assert.That(response.PromptTokens, Is.GreaterThan(0));
            Assert.That(response.CompletionTokens, Is.GreaterThan(0));
            Assert.That(response.RawRequestJson, Does.Contain(@"""reasoning_effort"":""low"""));
        });
    }
}
