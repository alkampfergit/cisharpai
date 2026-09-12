using System.Text.Json;
using Cisharpai.Azure;
using Cisharpai.Azure.AzureAiInference;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.AzureAiInference;

public sealed class AzureAiInferenceJsonOutputIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
        var raw = Environment.GetEnvironmentVariable(DotEnv.AzureAiInferenceTestModels);
        Assert.That(raw, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AzureAiInferenceTestModels} must be set with comma-separated model IDs.");
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

    private IChatCompletionClient CreateClient(string modelId)
    {
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

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IChatCompletionClient>();
    }

    // --- Feature Discovery ---

    [TestCaseSource(nameof(Models))]
    public void FeatureDiscovery_JsonOutputFeature_Available(string modelId)
    {
        var client = CreateClient(modelId);
        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    // --- JSON Mode ---

    [TestCaseSource(nameof(Models))]
    public async Task JsonMode_ReturnsValidJson(string modelId)
    {
        var client = CreateClient(modelId);
        var jsonFeature = client.Features.Get<IJsonOutputFeature>()!;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "List 3 colors with hex codes")],
            Model: modelId,
            Temperature: 0,
            MaxTokens: 500);

        var options = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);

        // Verify content is valid JSON
        Assert.DoesNotThrow(() => JsonDocument.Parse(response.Content));
        Assert.That(response.PromptTokens, Is.GreaterThanOrEqualTo(0));
        Assert.That(response.CompletionTokens, Is.GreaterThanOrEqualTo(0));
    }

    // --- Structured Outputs ---

    [TestCaseSource(nameof(Models))]
    public async Task StructuredOutputs_SimpleSchema_MatchesSchema(string modelId)
    {
        var client = CreateClient(modelId);
        var jsonFeature = client.Features.Get<IJsonOutputFeature>()!;

        const string schema = """
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string" },
                    "age": { "type": "integer" }
                },
                "required": ["name", "age"],
                "additionalProperties": false
            }
            """;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Generate a fictional person with a name and age")],
            Model: modelId,
            Temperature: 0,
            MaxTokens: 500);

        var options = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            SchemaDescription: "A person with name and age",
            JsonSchema: schema);

        var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);

        // Not all Azure AI Inference models support json_schema
        if (!response.IsSuccess)
        {
            Assert.Warn($"Structured outputs not supported on model '{modelId}': {response.ErrorMessage}");
            return;
        }

        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);

        var doc = JsonDocument.Parse(response.Content);
        Assert.That(doc.RootElement.TryGetProperty("name", out var name), Is.True);
        Assert.That(name.GetString(), Is.Not.Null.And.Not.Empty);
        Assert.That(doc.RootElement.TryGetProperty("age", out var age), Is.True);
        Assert.That(age.ValueKind, Is.EqualTo(JsonValueKind.Number));
    }
}
