using Cisharpai.Azure;
using Cisharpai.Azure.AzureAiInference;
using Cisharpai.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.AzureAiInference;

public sealed class AzureAiInferenceChatCompletionIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    private static IEnumerable<string> Models()
    {
        DotEnv.Load();
        var raw = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestModels);
        if (string.IsNullOrWhiteSpace(raw))
        {
            yield return "__MISSING_MODELS__";
            yield break;
        }

        foreach (var model in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return model;
    }

    [TestCaseSource(nameof(Models))]
    public async Task GetChatCompletionAsync_ReturnsValidResponse(string modelId)
    {
        Assert.That(modelId, Is.Not.EqualTo("__MISSING_MODELS__"),
            $"Environment variable {DotEnv.AzureAiInferenceTestModels} must be set.");

        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestApiKey);

        Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureAiInferenceTestEndpoint} must be set.");
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureAiInferenceTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureAiInferenceChatCompletion(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = apiKey!;
            options.ModelId = modelId;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Reply with exactly: hello")],
            Model: modelId,
            Temperature: 0,
            MaxTokens: 8000,
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Content.ToLowerInvariant(), Does.Contain("hello"));
        Assert.That(response.Model, Is.Not.Null.And.Not.Empty);
        Assert.That(response.PromptTokens, Is.GreaterThanOrEqualTo(0));
        Assert.That(response.CompletionTokens, Is.GreaterThanOrEqualTo(0));
    }

    [TestCaseSource(nameof(Models))]
    public async Task GetChatCompletionAsync_WithSystemMessage_ReturnsValidResponse(string modelId)
    {
        Assert.That(modelId, Is.Not.EqualTo("__MISSING_MODELS__"),
            $"Environment variable {DotEnv.AzureAiInferenceTestModels} must be set.");

        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestApiKey);

        Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureAiInferenceTestEndpoint} must be set.");
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureAiInferenceTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureAiInferenceChatCompletion(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = apiKey!;
            options.ModelId = modelId;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.System, "You are a helpful assistant that always responds in exactly 3 words."),
                new LlmMessage(LlmRole.User, "What is 2+2?")
            ],
            Model: modelId,
            Temperature: 0,
            MaxTokens: 5000,
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
    }

    [TestCaseSource(nameof(Models))]
    public async Task GetChatCompletionAsync_MultiTurnConversation_ReturnsValidResponse(string modelId)
    {
        Assert.That(modelId, Is.Not.EqualTo("__MISSING_MODELS__"),
            $"Environment variable {DotEnv.AzureAiInferenceTestModels} must be set.");

        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestApiKey);

        Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureAiInferenceTestEndpoint} must be set.");
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureAiInferenceTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureAiInferenceChatCompletion(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = apiKey!;
            options.ModelId = modelId;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.User, "My name is Alice"),
                new LlmMessage(LlmRole.Assistant, "Hello Alice! Nice to meet you."),
                new LlmMessage(LlmRole.User, "What is my name?")
            ],
            Model: modelId,
            Temperature: 0,
            MaxTokens: 8192,
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Content, Does.Contain("Alice").IgnoreCase);
    }

    [TestCaseSource(nameof(Models))]
    public async Task GetChatCompletionAsync_InvalidApiKey_ReturnsErrorResponse(string modelId)
    {
        Assert.That(modelId, Is.Not.EqualTo("__MISSING_MODELS__"),
            $"Environment variable {DotEnv.AzureAiInferenceTestModels} must be set.");

        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestEndpoint);

        Assert.That(endpoint, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureAiInferenceTestEndpoint} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureAiInferenceChatCompletion(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = "invalid-api-key";
            options.ModelId = modelId;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatCompletionClient>();

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: modelId);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }
}
