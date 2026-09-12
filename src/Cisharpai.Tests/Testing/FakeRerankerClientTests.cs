using Cisharpai.Models;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Testing;

public sealed class FakeRerankerClientTests
{
    private static readonly string[] Documents = ["a", "b", "c"];

    [Test]
    public async Task RerankAsync_ReturnsQueuedResponsesInOrder()
    {
        var fake = new FakeRerankerClient();
        fake.EnqueueResponse(FakeResponses.Rerank((0, 0.9)));
        fake.EnqueueResponse(FakeResponses.Rerank((2, 0.5)));

        var first = await fake.RerankAsync(new RerankRequest("q", Documents));
        var second = await fake.RerankAsync(new RerankRequest("q", Documents));

        Assert.Multiple(() =>
        {
            Assert.That(first.Results[0].Index, Is.EqualTo(0));
            Assert.That(second.Results[0].Index, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task RerankAsync_FallsBackToDefaultResponseWhenQueueIsEmpty()
    {
        var fake = new FakeRerankerClient
        {
            DefaultResponse = FakeResponses.Rerank(3)
        };

        var response = await fake.RerankAsync(new RerankRequest("q", Documents));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Results, Has.Count.EqualTo(3));
            Assert.That(response.Results[0].RelevanceScore, Is.GreaterThan(response.Results[1].RelevanceScore));
        });
    }

    [Test]
    public async Task RerankAsync_CapturesReceivedRequests()
    {
        var fake = new FakeRerankerClient { DefaultResponse = FakeResponses.Rerank(1) };

        await fake.RerankAsync(new RerankRequest("what is the capital?", Documents, TopN: 2));

        Assert.Multiple(() =>
        {
            Assert.That(fake.ReceivedRequests, Has.Count.EqualTo(1));
            Assert.That(fake.ReceivedRequests[0].Query, Is.EqualTo("what is the capital?"));
            Assert.That(fake.ReceivedRequests[0].TopN, Is.EqualTo(2));
            Assert.That(fake.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void RerankAsync_ThrowsWhenNoResponseIsConfigured()
    {
        var fake = new FakeRerankerClient();

        Assert.That(
            async () => await fake.RerankAsync(new RerankRequest("q", Documents)),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task Reset_ClearsQueueAndCapturedRequests()
    {
        var fake = new FakeRerankerClient();
        fake.EnqueueResponse(FakeResponses.Rerank(1));
        await fake.RerankAsync(new RerankRequest("q", Documents));

        fake.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(fake.ReceivedRequests, Is.Empty);
            Assert.That(fake.CallCount, Is.Zero);
        });

        Assert.That(
            async () => await fake.RerankAsync(new RerankRequest("q", Documents)),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task RerankError_ProducesFailedResponse()
    {
        var fake = new FakeRerankerClient();
        fake.EnqueueResponse(FakeResponses.RerankError("boom"));

        var response = await fake.RerankAsync(new RerankRequest("q", Documents));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Is.EqualTo("boom"));
            Assert.That(response.Results, Is.Empty);
        });
    }
}
