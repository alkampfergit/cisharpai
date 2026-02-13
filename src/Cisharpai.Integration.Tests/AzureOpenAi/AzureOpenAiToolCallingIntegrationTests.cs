using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Azure;
using Cisharpai.Azure.AzureOpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.AzureOpenAi;

public sealed class AzureOpenAiToolCallingIntegrationTests
{
    private static readonly JsonElement WeatherParameters = JsonDocument.Parse(
        """{"type":"object","properties":{"city":{"type":"string","description":"The city name"}},"required":["city"],"additionalProperties":false}""")
        .RootElement.Clone();

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

    private static IChatCompletionClient CreateClient(string deployment)
    {
        var endpoint = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestEndpoint);
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AzureOpenAiTestApiKey);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureOpenAiClient(options =>
        {
            options.Endpoint = endpoint!;
            options.ApiKey = apiKey!;
            options.DeploymentName = deployment;
        });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IChatCompletionClient>();
    }

    // --- Feature Discovery ---

    [Test]
    public void FeatureDiscovery_ToolCallingFeature_Available()
    {
        var deployments = Deployments().ToList();
        var client = CreateClient(deployments.First());

        var feature = client.Features.Get<IToolCallingFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    // --- Single Tool Call ---

    [TestCaseSource(nameof(Deployments))]
    public async Task ToolCalling_SingleToolCall_ReturnsToolCall(string deployment)
    {
        var client = CreateClient(deployment);
        var toolFeature = client.Features.Get<IToolCallingFeature>()!;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the current weather in Paris?")],
            Model: deployment,
            Temperature: 0,
            MaxTokens: 1024);

        var toolOptions = new ToolCallingOptions(
            Tools: [new ToolDefinition("get_weather", "Get the current weather for a city", WeatherParameters)]);

        var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.ToolCalls, Is.Not.Null);
        Assert.That(response.ToolCalls!.Count, Is.GreaterThanOrEqualTo(1));

        var toolCall = response.ToolCalls[0];
        Assert.That(toolCall.FunctionName, Is.EqualTo("get_weather"));
        Assert.That(toolCall.Id, Is.Not.Null.And.Not.Empty);
        Assert.That(toolCall.Arguments.GetProperty("city").GetString(), Is.Not.Null.And.Not.Empty);
    }

    // --- ToolChoice.Required Forces Tool Call ---

    [TestCaseSource(nameof(Deployments))]
    public async Task ToolCalling_ToolChoiceRequired_ForcesToolCall(string deployment)
    {
        var client = CreateClient(deployment);
        var toolFeature = client.Features.Get<IToolCallingFeature>()!;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello, how are you?")],
            Model: deployment,
            Temperature: 0,
            MaxTokens: 1024);

        var toolOptions = new ToolCallingOptions(
            Tools: [new ToolDefinition("get_weather", "Get the current weather for a city", WeatherParameters)],
            ToolChoice: ToolChoice.Required);

        var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.ToolCalls, Is.Not.Null, "ToolChoice.Required should force a tool call");
        Assert.That(response.ToolCalls!.Count, Is.GreaterThanOrEqualTo(1));
    }

    // --- Full Multi-turn Tool Call Loop ---

    [TestCaseSource(nameof(Deployments))]
    public async Task ToolCalling_MultiTurnLoop_ReturnsTextResponseAfterToolResult(string deployment)
    {
        var client = CreateClient(deployment);
        var toolFeature = client.Features.Get<IToolCallingFeature>()!;

        // Step 1: Send initial request with tools
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the current weather in Paris?")],
            Model: deployment,
            Temperature: 0,
            MaxTokens: 1024);

        var toolOptions = new ToolCallingOptions(
            Tools: [new ToolDefinition("get_weather", "Get the current weather for a city", WeatherParameters)]);

        var response = await toolFeature.GetChatCompletionWithToolsAsync(request, toolOptions);

        Assert.That(response.IsSuccess, Is.True, $"Step 1 failed: {response.ErrorMessage}");
        Assert.That(response.ToolCalls, Is.Not.Null, "Expected tool call in step 1");

        var toolCall = response.ToolCalls![0];

        // Step 2: Send tool results back
        var messages = new List<LlmMessage>
        {
            new(LlmRole.User, "What is the current weather in Paris?"),
            new(LlmRole.Assistant, "", ToolCalls: [toolCall]),
            new(LlmRole.Tool, "Sunny, 22 degrees Celsius", ToolCallId: toolCall.Id)
        };

        var followUpRequest = new ChatCompletionRequest(
            Messages: messages,
            Model: deployment,
            Temperature: 0,
            MaxTokens: 1024);

        var finalResponse = await toolFeature.GetChatCompletionWithToolsAsync(followUpRequest, toolOptions);

        Assert.That(finalResponse.IsSuccess, Is.True, $"Step 2 failed: {finalResponse.ErrorMessage}");
        Assert.That(finalResponse.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(finalResponse.Content.ToLowerInvariant(), Does.Contain("paris").Or.Contain("sunny").Or.Contain("22"));
    }
}
