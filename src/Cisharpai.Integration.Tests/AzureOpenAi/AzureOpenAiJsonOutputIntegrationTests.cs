using System.Text.Json;
using Cisharpai.Azure;
using Cisharpai.Azure.AzureOpenAi;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.AzureOpenAi;

public sealed class AzureOpenAiJsonOutputIntegrationTests
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

    private IChatCompletionClient CreateClient(string deployment)
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

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IChatCompletionClient>();
    }

    // --- Feature Discovery ---

    [TestCaseSource(nameof(Deployments))]
    public void FeatureDiscovery_JsonOutputFeature_Available(string deployment)
    {
        var client = CreateClient(deployment);
        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    // --- JSON Mode ---

    [TestCaseSource(nameof(Deployments))]
    public async Task JsonMode_ReturnsValidJson(string deployment)
    {
        var client = CreateClient(deployment);
        var jsonFeature = client.Features.Get<IJsonOutputFeature>()!;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "List 3 colors with hex codes")],
            Model: deployment,
            Temperature: 0,
            MaxTokens: 500);

        var options = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);

        // Verify content is valid JSON
        Assert.DoesNotThrow(() => JsonDocument.Parse(response.Content));
        Assert.That(response.PromptTokens, Is.GreaterThan(0));
        Assert.That(response.CompletionTokens, Is.GreaterThan(0));
    }

    // --- Structured Outputs ---

    [TestCaseSource(nameof(Deployments))]
    public async Task StructuredOutputs_SimpleSchema_MatchesSchema(string deployment)
    {
        var client = CreateClient(deployment);
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
            Model: deployment,
            Temperature: 0,
            MaxTokens: 500);

        var options = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            SchemaDescription: "A person with name and age",
            JsonSchema: schema);

        var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);

        // Some Azure deployments may not support json_schema
        if (!response.IsSuccess)
        {
            Assert.Warn($"Structured outputs not supported on deployment '{deployment}': {response.ErrorMessage}");
            return;
        }

        Assert.That(response.Refusal, Is.Null);
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);

        var doc = JsonDocument.Parse(response.Content);
        Assert.That(doc.RootElement.TryGetProperty("name", out var name), Is.True);
        Assert.That(name.GetString(), Is.Not.Null.And.Not.Empty);
        Assert.That(doc.RootElement.TryGetProperty("age", out var age), Is.True);
        Assert.That(age.ValueKind, Is.EqualTo(JsonValueKind.Number));
    }
}
