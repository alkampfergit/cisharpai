using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.OpenAi;

public sealed class OpenAiJsonOutputIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    private IChatCompletionClient CreateClient()
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.OpenAiTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.OpenAiTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenAiClient(options => { options.ApiKey = apiKey!; });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IChatCompletionClient>();
    }

    // --- Feature Discovery ---

    [Test]
    public void FeatureDiscovery_JsonOutputFeature_Available()
    {
        var client = CreateClient();

        var feature = client.Features.Get<IJsonOutputFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    // --- JSON Mode ---

    [TestCase("gpt-4.1-nano")]
    [TestCase("gpt-5-nano")]
    public async Task JsonMode_ReturnsValidJson(string model)
    {
        var client = CreateClient();
        var jsonFeature = client.Features.Get<IJsonOutputFeature>()!;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "List 3 colors with hex codes")],
            Model: model,
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

    [TestCase("gpt-4.1-nano")]
    [TestCase("gpt-5-nano")]
    public async Task StructuredOutputs_SimpleSchema_MatchesSchema(string model)
    {
        var client = CreateClient();
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
            Model: model,
            Temperature: 0,
            MaxTokens: 5000);

        var options = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "person",
            SchemaDescription: "A person with name and age",
            JsonSchema: schema);

        var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Refusal, Is.Null);
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);

        // Parse and verify schema compliance
        var doc = JsonDocument.Parse(response.Content);
        Assert.That(doc.RootElement.TryGetProperty("name", out var name), Is.True);
        Assert.That(name.GetString(), Is.Not.Null.And.Not.Empty);
        Assert.That(doc.RootElement.TryGetProperty("age", out var age), Is.True);
        Assert.That(age.ValueKind, Is.EqualTo(JsonValueKind.Number));
    }

    [Test]
    public async Task StructuredOutputs_ComplexSchema_ReturnsValidOutput()
    {
        var client = CreateClient();
        var jsonFeature = client.Features.Get<IJsonOutputFeature>()!;

        const string schema = """
            {
                "type": "object",
                "properties": {
                    "colors": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "name": { "type": "string" },
                                "hex": { "type": "string" }
                            },
                            "required": ["name", "hex"],
                            "additionalProperties": false
                        }
                    }
                },
                "required": ["colors"],
                "additionalProperties": false
            }
            """;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "List 3 colors with their hex codes")],
            Model: "gpt-4.1-nano",
            Temperature: 0,
            MaxTokens: 500);

        var options = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "color_list",
            SchemaDescription: "A list of colors with hex codes",
            JsonSchema: schema);

        var response = await jsonFeature.GetChatCompletionWithJsonOutputAsync(request, options);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);

        var doc = JsonDocument.Parse(response.Content);
        Assert.That(doc.RootElement.TryGetProperty("colors", out var colors), Is.True);
        Assert.That(colors.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(colors.GetArrayLength(), Is.EqualTo(3));

        foreach (var color in colors.EnumerateArray())
        {
            Assert.That(color.TryGetProperty("name", out _), Is.True);
            Assert.That(color.TryGetProperty("hex", out _), Is.True);
        }
    }
}
