using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Testing;

public sealed class PromptCachingFakeTests
{
    private static readonly int[] ExpectedBreakpoints = [0, 2];

    [Test]
    public void FakeChatCompletionClient_RegistersPromptCachingFeature_ByDefault()
    {
        var client = new FakeChatCompletionClient();
        var feature = client.Features.Get<IPromptCachingFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    [Test]
    public void FakeChatCompletionClient_DoesNotRegisterPromptCachingFeature_WhenExcluded()
    {
        var client = new FakeChatCompletionClient(
            FakeChatFeatures.Streaming | FakeChatFeatures.ToolCalling);
        var feature = client.Features.Get<IPromptCachingFeature>();

        Assert.That(feature, Is.Null);
    }

    [Test]
    public async Task FakeChatCompletionClient_PromptCaching_ReturnsQueuedResponse()
    {
        var client = new FakeChatCompletionClient();
        var expectedResponse = FakeResponses.CachedChat("Hello", cachedInputTokens: 500, cacheCreationInputTokens: 200);
        client.EnqueuePromptCachingResponse(expectedResponse);

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var response = await feature.GetChatCompletionWithCachingAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Hi")],
                Model: "test"),
            new PromptCachingOptions { CacheSystemMessage = true });

        Assert.Multiple(() =>
        {
            Assert.That(response.Content, Is.EqualTo("Hello"));
            Assert.That(response.CachedInputTokens, Is.EqualTo(500));
            Assert.That(response.CacheCreationInputTokens, Is.EqualTo(200));
        });
    }

    [Test]
    public async Task FakeChatCompletionClient_PromptCaching_CapturesRequests()
    {
        var client = new FakeChatCompletionClient();
        client.DefaultPromptCachingResponse = FakeResponses.CachedChat("cached", cachedInputTokens: 100);

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        var cachingOptions = new PromptCachingOptions
        {
            CacheSystemMessage = true,
            MessageBreakpoints = [0, 2]
        };

        await feature.GetChatCompletionWithCachingAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Hi")],
                Model: "test"),
            cachingOptions);

        Assert.That(client.ReceivedPromptCachingRequests, Has.Count.EqualTo(1));
        Assert.That(client.ReceivedPromptCachingRequests[0].Options.CacheSystemMessage, Is.True);
        Assert.That(client.ReceivedPromptCachingRequests[0].Options.MessageBreakpoints, Is.EqualTo(ExpectedBreakpoints));
    }

    [Test]
    public void FakeResponses_CachedChat_SetsFields()
    {
        var response = FakeResponses.CachedChat("test", cachedInputTokens: 300, cacheCreationInputTokens: 150);

        Assert.Multiple(() =>
        {
            Assert.That(response.Content, Is.EqualTo("test"));
            Assert.That(response.CachedInputTokens, Is.EqualTo(300));
            Assert.That(response.CacheCreationInputTokens, Is.EqualTo(150));
            Assert.That(response.IsSuccess, Is.True);
        });
    }

    [Test]
    public void FakeResponses_CachedChat_WithoutCreation_HasNullCreation()
    {
        var response = FakeResponses.CachedChat("test", cachedInputTokens: 300);

        Assert.Multiple(() =>
        {
            Assert.That(response.CachedInputTokens, Is.EqualTo(300));
            Assert.That(response.CacheCreationInputTokens, Is.Null);
        });
    }

    [Test]
    public async Task FakeChatCompletionClient_Reset_ClearsPromptCachingState()
    {
        var client = new FakeChatCompletionClient();
        client.EnqueuePromptCachingResponse(FakeResponses.CachedChat("cached", cachedInputTokens: 100));

        var feature = client.Features.Get<IPromptCachingFeature>()!;
        await feature.GetChatCompletionWithCachingAsync(
            new ChatCompletionRequest(
                Messages: [new LlmMessage(LlmRole.User, "Hi")],
                Model: "test"),
            new PromptCachingOptions());

        client.Reset();

        Assert.That(client.ReceivedPromptCachingRequests, Is.Empty);
        Assert.That(client.CallCount, Is.EqualTo(0));
    }
}
