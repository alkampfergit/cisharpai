using Cisharpai.Rag.Pipeline;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag.Pipeline;

[TestFixture]
public class FakeRagPipelineTests
{
    [Test]
    public async Task AskAsync_QueuedResponse_IsReturned()
    {
        var fake = new FakeRagPipeline();
        var expected = new RagResult { Answer = "answer" };
        fake.EnqueueResponse(expected);

        var result = await fake.AskAsync("query");

        Assert.That(result.Answer, Is.EqualTo("answer"));
        Assert.That(fake.ReceivedQueries, Has.Count.EqualTo(1));
        Assert.That(fake.ReceivedQueries[0].Query, Is.EqualTo("query"));
    }

    [Test]
    public async Task AskAsync_DefaultResponse_IsReturned()
    {
        var fake = new FakeRagPipeline
        {
            DefaultResponse = new RagResult { Answer = "default" }
        };

        var result = await fake.AskAsync("query");

        Assert.That(result.Answer, Is.EqualTo("default"));
    }

    [Test]
    public void AskAsync_NoResponseConfigured_Throws()
    {
        var fake = new FakeRagPipeline();
        Assert.ThrowsAsync<InvalidOperationException>(() => fake.AskAsync("query"));
    }

    [Test]
    public async Task AskStreamingAsync_QueuedResponse_IsReturned()
    {
        var fake = new FakeRagPipeline();
        fake.EnqueueStreamingResponse(FakeResponses.RagStreamingChunks("Hello ", "World"));

        var chunks = new List<RagStreamingChunk>();
        await foreach (var chunk in fake.AskStreamingAsync("query"))
            chunks.Add(chunk);

        Assert.That(chunks, Has.Count.EqualTo(2));
        Assert.That(fake.ReceivedStreamingQueries, Has.Count.EqualTo(1));
    }

    [Test]
    public void Reset_ClearsEverything()
    {
        var fake = new FakeRagPipeline();
        fake.EnqueueResponse(new RagResult { Answer = "a" });
        fake.EnqueueStreamingResponse(FakeResponses.RagStreamingChunks("a"));

        fake.Reset();

        Assert.Throws<InvalidOperationException>(() => fake.AskAsync("q").GetAwaiter().GetResult());
    }

    [Test]
    public async Task CallCount_TracksAllCalls()
    {
        var fake = new FakeRagPipeline
        {
            DefaultResponse = new RagResult { Answer = "a" },
            DefaultStreamingResponse = FakeResponses.RagStreamingChunks("a")
        };

        await fake.AskAsync("q1");
        await foreach (var _ in fake.AskStreamingAsync("q2")) { }

        Assert.That(fake.CallCount, Is.EqualTo(2));
    }
}
