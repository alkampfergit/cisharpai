using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Anthropic;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.Anthropic;

public sealed class AnthropicGroundedChatIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    private static IChatCompletionClient CreateClient()
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AnthropicTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AnthropicTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnthropicClient(options => { options.ApiKey = apiKey!; });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IChatCompletionClient>();
    }

    [Test]
    public void FeatureDiscovery_GroundedChatFeature_Available()
    {
        var client = CreateClient();

        var feature = client.Features.Get<IGroundedChatFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    [Test]
    public async Task GroundedChat_WithPlainTextDocuments_ReturnsCitations()
    {
        var client = CreateClient();
        var groundedFeature = client.Features.Get<IGroundedChatFeature>()!;

        var documents = new List<DocumentChunk>
        {
            new(Id: "doc-1", Text: "Paris is the capital city of France. It is located in northern France on the Seine river."),
            new(Id: "doc-2", Text: "Berlin is the capital city of Germany. It is the largest city in Germany.")
        };

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the capital of France? Answer in one sentence.")],
            Model: "claude-sonnet-4-5-20250929",
            Temperature: 0,
            MaxTokens: 500,
            IncludeRawResponse: true);

        var options = new GroundedChatOptions(Documents: documents, CitationMode: CitationMode.Enabled);

        var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Content.ToLowerInvariant(), Does.Contain("paris"));
        Assert.That(response.Citations, Is.Not.Empty);
        Assert.That(response.Citations[0].Sources, Is.Not.Empty);
        Assert.That(response.Citations[0].Sources[0].Id, Is.EqualTo("doc-1"));
        Assert.That(response.Citations[0].Sources[0].CitedText, Is.Not.Null.And.Not.Empty);
        Assert.That(response.ChatCompletion.PromptTokens, Is.GreaterThan(0));
        Assert.That(response.ChatCompletion.CompletionTokens, Is.GreaterThan(0));
        Assert.That(response.ChatCompletion.RawResponseJson, Is.Not.Null.And.Not.Empty);
        Assert.That(response.ChatCompletion.RawRequestJson, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task GroundedChat_WithKeyValueDocuments_ReturnsCitations()
    {
        var client = CreateClient();
        var groundedFeature = client.Features.Get<IGroundedChatFeature>()!;

        var documents = new List<DocumentChunk>
        {
            new(Id: "doc-1", Data: new Dictionary<string, string>
            {
                ["title"] = "France",
                ["snippet"] = "Paris is the capital city of France. It is located in northern France on the Seine river."
            }),
            new(Id: "doc-2", Data: new Dictionary<string, string>
            {
                ["title"] = "Germany",
                ["snippet"] = "Berlin is the capital city of Germany. It is the largest city in Germany."
            })
        };

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the capital of Germany? Answer in one sentence.")],
            Model: "claude-sonnet-4-5-20250929",
            Temperature: 0,
            MaxTokens: 500);

        var options = new GroundedChatOptions(Documents: documents, CitationMode: CitationMode.Enabled);

        var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Content.ToLowerInvariant(), Does.Contain("berlin"));
        Assert.That(response.Citations, Is.Not.Empty);
    }

    [Test]
    public async Task GroundedChat_CitationOffsetsAreValid()
    {
        var client = CreateClient();
        var groundedFeature = client.Features.Get<IGroundedChatFeature>()!;

        var documents = new List<DocumentChunk>
        {
            new(Id: "doc-1", Text: "Paris is the capital city of France.")
        };

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the capital of France? Reply in one sentence.")],
            Model: "claude-sonnet-4-5-20250929",
            Temperature: 0,
            MaxTokens: 500);

        var options = new GroundedChatOptions(Documents: documents, CitationMode: CitationMode.Enabled);

        var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Citations, Is.Not.Empty);

        foreach (var citation in response.Citations)
        {
            Assert.That(citation.Start, Is.GreaterThanOrEqualTo(0));
            Assert.That(citation.End, Is.GreaterThan(citation.Start));
            Assert.That(citation.End, Is.LessThanOrEqualTo(response.Content.Length));
        }
    }
}
