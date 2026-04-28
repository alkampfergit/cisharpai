using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Cohere;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.Cohere;

public sealed class CohereGroundedChatIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    private static IChatCompletionClient CreateClient()
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
    public void FeatureDiscovery_GroundedChatFeature_Available()
    {
        var client = CreateClient();

        var feature = client.Features.Get<IGroundedChatFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    // --- Grounded Chat with Key-Value Documents ---

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
            Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
            Model: "command-a-03-2025",
            Temperature: 0,
            MaxTokens: 500);

        var options = new GroundedChatOptions(Documents: documents);

        var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Content.ToLowerInvariant(), Does.Contain("paris"));
        Assert.That(response.Citations, Is.Not.Empty);
        Assert.That(response.Citations[0].Sources, Is.Not.Empty);
        Assert.That(response.ChatCompletion.PromptTokens, Is.GreaterThan(0));
        Assert.That(response.ChatCompletion.CompletionTokens, Is.GreaterThan(0));
    }

    // --- Grounded Chat with Plain Text Documents ---

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
            Messages: [new LlmMessage(LlmRole.User, "What is the capital of Germany?")],
            Model: "command-a-03-2025",
            Temperature: 0,
            MaxTokens: 500);

        var options = new GroundedChatOptions(Documents: documents);

        var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Content.ToLowerInvariant(), Does.Contain("berlin"));
        Assert.That(response.Citations, Is.Not.Empty);
    }

    // --- Citation Offsets Match Content ---

    [Test]
    public async Task GroundedChat_CitationOffsetsMatchContent()
    {
        var client = CreateClient();
        var groundedFeature = client.Features.Get<IGroundedChatFeature>()!;

        var documents = new List<DocumentChunk>
        {
            new(Id: "doc-1", Data: new Dictionary<string, string>
            {
                ["title"] = "France",
                ["snippet"] = "Paris is the capital city of France."
            })
        };

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
            Model: "command-a-03-2025",
            Temperature: 0,
            MaxTokens: 500);

        var options = new GroundedChatOptions(
            Documents: documents,
            CitationMode: CitationMode.Accurate);

        var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Citations, Is.Not.Empty);

        foreach (var citation in response.Citations)
        {
            Assert.That(citation.Start, Is.GreaterThanOrEqualTo(0));
            Assert.That(citation.End, Is.GreaterThan(citation.Start));
            Assert.That(citation.End, Is.LessThanOrEqualTo(response.Content.Length));

            var extractedText = response.Content[citation.Start..citation.End];
            Assert.That(extractedText, Is.EqualTo(citation.Text),
                $"Citation offset mismatch: expected '{citation.Text}' but got '{extractedText}' at [{citation.Start}..{citation.End}]");
        }
    }

    // --- Fast Mode ---

    [Test]
    public async Task GroundedChat_FastMode_ReturnsValidResponse()
    {
        var client = CreateClient();
        var groundedFeature = client.Features.Get<IGroundedChatFeature>()!;

        var documents = new List<DocumentChunk>
        {
            new(Id: "doc-1", Text: "The Eiffel Tower is located in Paris, France. It was built in 1889.")
        };

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "When was the Eiffel Tower built?")],
            Model: "command-a-03-2025",
            Temperature: 0,
            MaxTokens: 500);

        var options = new GroundedChatOptions(
            Documents: documents,
            CitationMode: CitationMode.Fast);

        var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);

        Assert.That(response.IsSuccess, Is.True, $"Request failed: {response.ErrorMessage}");
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Content.ToLowerInvariant(), Does.Contain("1889"));
    }
}
