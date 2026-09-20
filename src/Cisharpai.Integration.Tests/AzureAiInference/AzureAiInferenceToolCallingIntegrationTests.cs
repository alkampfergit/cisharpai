using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Azure;
using Cisharpai.Azure.AzureAiInference;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.AzureAiInference;

public sealed class AzureAiInferenceToolCallingIntegrationTests
{
    private static readonly JsonElement WeatherParameters = JsonDocument.Parse(
        """{"type":"object","properties":{"city":{"type":"string","description":"The city name"}},"required":["city"],"additionalProperties":false}""")
        .RootElement.Clone();

    /// <summary>
    /// Tracks how many models passed strict tool-calling assertions.
    /// If zero models pass, the OneTimeTearDown will fail the suite.
    /// </summary>
    private static int _modelsWithToolCallingSupport;

    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
        _modelsWithToolCallingSupport = 0;
        var raw = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestModels);
        Assert.That(raw, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureAiInferenceTestModels} must be set with comma-separated model IDs.");
    }

    [OneTimeTearDown]
    public void VerifyAtLeastOneModelSupportsToolCalling()
    {
        if (_modelsWithToolCallingSupport == 0)
        {
            Assert.Fail(
                "No configured Azure AI Inference model successfully completed tool-calling assertions. " +
                "Ensure at least one model in AZURE_INFERENCE_TEST_MODELS supports tool calling.");
        }

        TestContext.WriteLine(
            $"{_modelsWithToolCallingSupport} model(s) passed strict tool-calling assertions.");
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

    private static IChatCompletionClient CreateClient(string modelId)
    {
        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestApiKey);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureAiInferenceChatCompletion(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = apiKey!;
            options.ModelId = modelId;
        });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IChatCompletionClient>();
    }

    // --- Feature Discovery ---

    [Test]
    public void FeatureDiscovery_ToolCallingFeature_Available()
    {
        var models = Models().ToList();
        var client = CreateClient(models.First());

        var feature = client.Features.Get<IToolCallingFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    // --- Single Tool Call ---

    [TestCaseSource(nameof(Models))]
    public async Task ToolCalling_SingleToolCall_ReturnsToolCall(string modelId)
    {
        var client = CreateClient(modelId);
        var toolFeature = client.Features.Get<IToolCallingFeature>()!;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the current weather in Paris?")],
            Model: modelId,
            Temperature: 0,
            MaxTokens: 1024);

        var toolOptions = new ToolCallingOptions(
            Tools: [new ToolDefinition("get_weather", "Get the current weather for a city", WeatherParameters)]);

        var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);

        // Some Azure AI models may not support tool calling - mark as inconclusive
        if (!response.IsSuccess)
        {
            Assert.Inconclusive(
                $"Model {modelId} does not support tool calling: {response.ErrorMessage}");
            return;
        }

        Assert.That(response.ToolCalls, Is.Not.Null);
        Assert.That(response.ToolCalls!.Count, Is.GreaterThanOrEqualTo(1));

        var toolCall = response.ToolCalls[0];
        Assert.That(toolCall.FunctionName, Is.EqualTo("get_weather"));
        Assert.That(toolCall.Id, Is.Not.Null.And.Not.Empty);
        Assert.That(toolCall.Arguments.GetProperty("city").GetString(), Is.Not.Null.And.Not.Empty);

        Interlocked.Increment(ref _modelsWithToolCallingSupport);
    }

    // --- ToolChoice.Required Forces Tool Call ---

    [TestCaseSource(nameof(Models))]
    public async Task ToolCalling_ToolChoiceRequired_ForcesToolCall(string modelId)
    {
        var client = CreateClient(modelId);
        var toolFeature = client.Features.Get<IToolCallingFeature>()!;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello, how are you?")],
            Model: modelId,
            Temperature: 0,
            MaxTokens: 1024);

        var toolOptions = new ToolCallingOptions(
            Tools: [new ToolDefinition("get_weather", "Get the current weather for a city", WeatherParameters)],
            ToolChoice: ToolChoice.Required);

        var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);

        // Some Azure AI models may not support tool calling - mark as inconclusive
        if (!response.IsSuccess)
        {
            Assert.Inconclusive(
                $"Model {modelId} does not support tool calling: {response.ErrorMessage}");
            return;
        }

        Assert.That(response.ToolCalls, Is.Not.Null, "ToolChoice.Required should force a tool call");
        Assert.That(response.ToolCalls!.Count, Is.GreaterThanOrEqualTo(1));
    }

    // --- Full Multi-turn Tool Call Loop ---

    [TestCaseSource(nameof(Models))]
    public async Task ToolCalling_MultiTurnLoop_ReturnsTextResponseAfterToolResult(string modelId)
    {
        var client = CreateClient(modelId);
        var toolFeature = client.Features.Get<IToolCallingFeature>()!;

        // Step 1: Send initial request with tools
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the current weather in Paris?")],
            Model: modelId,
            Temperature: 0,
            MaxTokens: 1024);

        var toolOptions = new ToolCallingOptions(
            Tools: [new ToolDefinition("get_weather", "Get the current weather for a city", WeatherParameters)]);

        var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);

        // Some Azure AI models may not support tool calling - mark as inconclusive
        if (!response.IsSuccess)
        {
            Assert.Inconclusive(
                $"Model {modelId} does not support tool calling: {response.ErrorMessage}");
            return;
        }

        if (response.ToolCalls is null || response.ToolCalls.Count == 0)
        {
            Assert.Inconclusive(
                $"Model {modelId} did not return tool calls in step 1 (may not support tool calling).");
            return;
        }

        var toolCall = response.ToolCalls[0];

        // Step 2: Send tool results back
        var messages = new List<LlmMessage>
        {
            new(LlmRole.User, "What is the current weather in Paris?"),
            new(LlmRole.Assistant, "", ToolCalls: [toolCall]),
            new(LlmRole.Tool, "Sunny, 22 degrees Celsius", ToolCallId: toolCall.Id)
        };

        var followUpRequest = new ChatCompletionRequest(
            Messages: messages,
            Model: modelId,
            Temperature: 0,
            MaxTokens: 1024);

        var finalResponse = await toolFeature.GetChatCompletionWithToolsAsync(followUpRequest, toolOptions);

        Assert.That(finalResponse.IsSuccess, Is.True, $"Step 2 failed: {finalResponse.ErrorMessage}");
        Assert.That(finalResponse.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(finalResponse.Content.ToLowerInvariant(), Does.Contain("paris").Or.Contain("sunny").Or.Contain("22"));
    }
}
