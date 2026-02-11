using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Cohere;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.Cohere;

public sealed class CohereToolCallingIntegrationTests
{
    private static readonly JsonElement WeatherParameters = JsonDocument.Parse(
        """{"type":"object","properties":{"city":{"type":"string","description":"The city name"}},"required":["city"]}""")
        .RootElement.Clone();

    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    private IChatCompletionClient CreateClient()
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.CohereTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.CohereTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCohereChatClient(options => { options.ApiKey = apiKey!; });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IChatCompletionClient>();
    }

    // --- Feature Discovery ---

    [Test]
    public void FeatureDiscovery_ToolCallingFeature_Available()
    {
        var client = CreateClient();

        var feature = client.Features.Get<IToolCallingFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    // --- Single Tool Call ---

    [TestCase("command-a-03-2025")]
    public async Task ToolCalling_SingleToolCall_ReturnsToolCall(string model)
    {
        var client = CreateClient();
        var toolFeature = client.Features.Get<IToolCallingFeature>()!;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the current weather in Paris?")],
            Model: model,
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

    [TestCase("command-a-03-2025")]
    public async Task ToolCalling_ToolChoiceRequired_ForcesToolCall(string model)
    {
        var client = CreateClient();
        var toolFeature = client.Features.Get<IToolCallingFeature>()!;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello, how are you?")],
            Model: model,
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

    [TestCase("command-a-03-2025")]
    public async Task ToolCalling_MultiTurnLoop_ReturnsTextResponseAfterToolResult(string model)
    {
        var client = CreateClient();
        var toolFeature = client.Features.Get<IToolCallingFeature>()!;

        // Step 1: Send initial request with tools
        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the current weather in Paris?")],
            Model: model,
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
            Model: model,
            Temperature: 0,
            MaxTokens: 1024);

        var finalResponse = await toolFeature.GetChatCompletionWithToolsAsync(followUpRequest, toolOptions);

        Assert.That(finalResponse.IsSuccess, Is.True, $"Step 2 failed: {finalResponse.ErrorMessage}");
        Assert.That(finalResponse.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(finalResponse.Content.ToLowerInvariant(), Does.Contain("paris").Or.Contain("sunny").Or.Contain("22"));
    }
}
