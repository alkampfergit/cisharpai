using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Testing;

public sealed class WebSearchFakeTests
{
    [Test]
    public async Task FakeClient_WebSearch_ReturnsQueuedResponse()
    {
        var client = new FakeChatCompletionClient();
        var expected = FakeResponses.WebSearch("Paris is the capital of France.");
        client.EnqueueWebSearchResponse(expected);

        var response = await client.GetChatCompletionWithWebSearchAsync(
            new ChatCompletionRequest(Messages: [new LlmMessage(LlmRole.User, "test")]),
            new WebSearchOptions());

        Assert.That(response, Is.SameAs(expected));
    }

    [Test]
    public async Task FakeClient_WebSearch_ReturnsDefaultResponse()
    {
        var client = new FakeChatCompletionClient();
        var expected = FakeResponses.WebSearch("default answer");
        client.DefaultWebSearchResponse = expected;

        var response = await client.GetChatCompletionWithWebSearchAsync(
            new ChatCompletionRequest(Messages: [new LlmMessage(LlmRole.User, "test")]),
            new WebSearchOptions());

        Assert.That(response, Is.SameAs(expected));
    }

    [Test]
    public async Task FakeClient_WebSearch_CapturesRequest()
    {
        var client = new FakeChatCompletionClient();
        client.DefaultWebSearchResponse = FakeResponses.WebSearch("answer");

        var request = new ChatCompletionRequest(Messages: [new LlmMessage(LlmRole.User, "What is AI?")]);
        var options = new WebSearchOptions();

        await client.GetChatCompletionWithWebSearchAsync(request, options);

        Assert.Multiple(() =>
        {
            Assert.That(client.ReceivedWebSearchRequests, Has.Count.EqualTo(1));
            Assert.That(client.ReceivedWebSearchRequests[0].Request, Is.SameAs(request));
            Assert.That(client.ReceivedWebSearchRequests[0].Options, Is.SameAs(options));
        });
    }

    [Test]
    public async Task FakeClient_WebSearch_IncludedInCallCount()
    {
        var client = new FakeChatCompletionClient();
        client.DefaultWebSearchResponse = FakeResponses.WebSearch("answer");

        await client.GetChatCompletionWithWebSearchAsync(
            new ChatCompletionRequest(Messages: [new LlmMessage(LlmRole.User, "test")]),
            new WebSearchOptions());

        Assert.That(client.CallCount, Is.EqualTo(1));
    }

    [Test]
    public void FakeClient_WebSearch_NoResponseConfigured_Throws()
    {
        var client = new FakeChatCompletionClient();

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetChatCompletionWithWebSearchAsync(
                new ChatCompletionRequest(Messages: [new LlmMessage(LlmRole.User, "test")]),
                new WebSearchOptions()));
    }

    [Test]
    public void FakeClient_WebSearchFeatureDisabled_ReturnsNull()
    {
        var client = new FakeChatCompletionClient(FakeChatFeatures.None);

        var feature = client.Features.Get<IWebSearchFeature>();

        Assert.That(feature, Is.Null);
    }

    [Test]
    public void FakeClient_WebSearchFeatureEnabled_ReturnsSelf()
    {
        var client = new FakeChatCompletionClient(FakeChatFeatures.WebSearch);

        var feature = client.Features.Get<IWebSearchFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public void FakeClient_AllFeatures_IncludesWebSearch()
    {
        var client = new FakeChatCompletionClient(FakeChatFeatures.All);

        var feature = client.Features.Get<IWebSearchFeature>();

        Assert.That(feature, Is.Not.Null);
    }

    [Test]
    public async Task FakeClient_Reset_ClearsWebSearchQueuesAndCapture()
    {
        var client = new FakeChatCompletionClient();
        client.DefaultWebSearchResponse = FakeResponses.WebSearch("default");

        await client.GetChatCompletionWithWebSearchAsync(
            new ChatCompletionRequest(Messages: [new LlmMessage(LlmRole.User, "test")]),
            new WebSearchOptions());

        Assert.That(client.ReceivedWebSearchRequests, Has.Count.EqualTo(1));

        client.Reset();

        Assert.That(client.ReceivedWebSearchRequests, Has.Count.EqualTo(0));
    }

    [Test]
    public void FakeResponses_WebSearch_SetsGroundingKind()
    {
        var response = FakeResponses.WebSearch("answer");

        Assert.That(response.GroundingKind, Is.EqualTo(GroundingKind.WebSearch));
    }

    [Test]
    public void FakeResponses_WebSearch_SetsWebSearchCount()
    {
        var response = FakeResponses.WebSearch("answer", webSearchCount: 3);

        Assert.That(response.ChatCompletion.WebSearchCount, Is.EqualTo(3));
    }

    [Test]
    public void FakeResponses_WebSearch_DefaultWebSearchCountIsOne()
    {
        var response = FakeResponses.WebSearch("answer");

        Assert.That(response.ChatCompletion.WebSearchCount, Is.EqualTo(1));
    }
}
