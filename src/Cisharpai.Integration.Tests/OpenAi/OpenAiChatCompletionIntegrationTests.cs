using System.Text.Json;
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

    [TestCase("gpt-4.1-nano")]
    [TestCase("gpt-5-nano")]
    public async Task GetChatCompletionAsync_ReturnsValidResponse(string model)
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
            Model: model,
            Temperature: 0,
            MaxTokens: 8000, //high number it must account reasoning tokens also
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);

        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Content.ToLowerInvariant(), Does.Contain("hello"));
        Assert.That(response.Model, Is.Not.Null.And.Not.Empty);
        Assert.That(response.PromptTokens, Is.GreaterThan(0));
        Assert.That(response.CompletionTokens, Is.GreaterThan(0));
        Assert.That(response.IsSuccess, Is.True);
        Assert.That(response.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5_VeryLowMaxTokens_ReturnsIncompleteErrorResponse()
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
            Messages: [new LlmMessage(LlmRole.User, "Write a very long and detailed essay about the history of computing")],
            Model: "gpt-5-nano",
            Temperature: 0,
            MaxTokens: 16,
            IncludeRawResponse: true);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Status, Is.EqualTo("incomplete"));
        Assert.That(response.IncompleteReason, Is.EqualTo("max_output_tokens"));
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5_ExtraParameters_VerbosityLow()
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

        var extra = JsonDocument.Parse("""{"text":{"verbosity":"low"}}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Explain how photosynthesis works")],
            Model: "gpt-5-nano",
            Temperature: 0,
            MaxTokens: 8000,
            IncludeRawResponse: true,
            ExtraParameters: extra);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.PromptTokens, Is.GreaterThan(0));
        Assert.That(response.CompletionTokens, Is.GreaterThan(0));

        // Verify the request JSON contains the verbosity parameter
        Assert.That(response.RawRequestJson, Is.Not.Null);
        var requestDoc = JsonDocument.Parse(response.RawRequestJson!);
        Assert.That(requestDoc.RootElement.GetProperty("text").GetProperty("verbosity").GetString(), Is.EqualTo("low"));
    }

    [Test]
    public async Task GetChatCompletionAsync_Gpt5_ExtraParameters_InvalidParameter_ReturnsError()
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

        var extra = JsonDocument.Parse("""{"totally_invalid_param_xyz":true}""").RootElement;
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-5-nano",
            Temperature: 0,
            MaxTokens: 100,
            IncludeRawResponse: true,
            ExtraParameters: extra);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }
}
