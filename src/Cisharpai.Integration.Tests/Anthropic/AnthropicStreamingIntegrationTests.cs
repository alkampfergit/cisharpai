using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Anthropic;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.Anthropic;

public sealed class AnthropicStreamingIntegrationTests
{
    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    private IChatCompletionClient CreateClient()
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
    public void Feature_Discovery_Returns_NonNull()
    {
        var client = CreateClient();

        var feature = client.Features.Get<IStreamingChatFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    [Test]
    public async Task Basic_Stream_Returns_Content()
    {
        var client = CreateClient();
        var streamFeature = client.Features.Get<IStreamingChatFeature>()!;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Say hello in one word")],
            Model: "claude-haiku-4-5-20251001",
            MaxTokens: 20);

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.That(chunks, Is.Not.Empty);

        var textChunks = chunks.Where(c => !string.IsNullOrEmpty(c.Content)).ToList();
        Assert.That(textChunks, Is.Not.Empty, "Should have at least one content chunk");

        var combined = string.Concat(chunks.Select(c => c.Content));
        Assert.That(combined, Is.Not.Null.And.Not.Empty, "Combined content should be non-empty");
    }

    [Test]
    public async Task Stream_Has_FinishReason()
    {
        var client = CreateClient();
        var streamFeature = client.Features.Get<IStreamingChatFeature>()!;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Say hi")],
            Model: "claude-haiku-4-5-20251001",
            MaxTokens: 10);

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var finishChunk = chunks.FirstOrDefault(c => c.FinishReason is not null);
        Assert.That(finishChunk, Is.Not.Null, "Should have at least one chunk with a finish reason");
    }

    [Test]
    public async Task Stream_Content_Accumulates_Coherently()
    {
        var client = CreateClient();
        var streamFeature = client.Features.Get<IStreamingChatFeature>()!;

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Count from 1 to 5, just the numbers separated by spaces")],
            Model: "claude-haiku-4-5-20251001",
            MaxTokens: 30);

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in streamFeature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var combined = string.Concat(chunks.Select(c => c.Content));
        Assert.That(combined, Is.Not.Empty);
        Assert.That(combined, Does.Contain("1"));
        Assert.That(combined, Does.Contain("5"));
    }
}
