using Cisharpai.Rag;
using Cisharpai.Rag.Models;
using Cisharpai.Rag.Packing;
using Cisharpai.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class FakeRetrieverTests
{
    private sealed record DummyProviderQuery(string Value) : IRetrievalQueryExtension;

    private static TextChunk MakeChunk(string docId, int index) =>
        new(docId, index, 0, 1, "x");

    [Test]
    public async Task DefaultResponse_ReturnedWhenQueueEmpty()
    {
        var chunks = new ScoredChunk[] { new(MakeChunk("doc", 0), 0.9) };
        var fake = new FakeRetriever { DefaultResponse = chunks };

        var result = await fake.RetrieveAsync("query", new RetrievalOptions { TopK = 5 });

        Assert.That(result, Is.EqualTo(chunks));
    }

    [Test]
    public async Task QueuedResponses_DequeuedInOrder()
    {
        var first = new ScoredChunk[] { new(MakeChunk("doc", 0), 0.9) };
        var second = new ScoredChunk[] { new(MakeChunk("doc", 1), 0.5) };

        var fake = new FakeRetriever();
        fake.EnqueueResponse(first);
        fake.EnqueueResponse(second);

        var r1 = await fake.RetrieveAsync("q1", new RetrievalOptions { TopK = 1 });
        var r2 = await fake.RetrieveAsync("q2", new RetrievalOptions { TopK = 1 });

        Assert.That(r1, Is.EqualTo(first));
        Assert.That(r2, Is.EqualTo(second));
    }

    [Test]
    public async Task QueueDrained_FallsBackToDefault()
    {
        var queued = new ScoredChunk[] { new(MakeChunk("doc", 0), 0.9) };
        var defaultResponse = new ScoredChunk[] { new(MakeChunk("doc", 1), 0.5) };

        var fake = new FakeRetriever { DefaultResponse = defaultResponse };
        fake.EnqueueResponse(queued);

        var r1 = await fake.RetrieveAsync("q1", new RetrievalOptions { TopK = 1 });
        var r2 = await fake.RetrieveAsync("q2", new RetrievalOptions { TopK = 1 });

        Assert.That(r1, Is.EqualTo(queued));
        Assert.That(r2, Is.EqualTo(defaultResponse));
    }

    [Test]
    public void NoQueueNoDefault_Throws()
    {
        var fake = new FakeRetriever();

        Assert.That(
            async () => await fake.RetrieveAsync("query", new RetrievalOptions { TopK = 5 }),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task ReceivedQueries_CapturesAll()
    {
        var fake = new FakeRetriever { DefaultResponse = Array.Empty<ScoredChunk>() };

        await fake.RetrieveAsync("first", new RetrievalOptions { TopK = 3 });
        await fake.RetrieveAsync("second", new RetrievalOptions { TopK = 7 });

        Assert.That(fake.ReceivedQueries, Has.Count.EqualTo(2));
        Assert.That(fake.ReceivedQueries[0].Query, Is.EqualTo("first"));
        Assert.That(fake.ReceivedQueries[0].Options.TopK, Is.EqualTo(3));
        Assert.That(fake.ReceivedQueries[1].Query, Is.EqualTo("second"));
        Assert.That(fake.ReceivedQueries[1].Options.TopK, Is.EqualTo(7));
        Assert.That(fake.CallCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Reset_ClearsQueueAndCapturedQueries()
    {
        var fake = new FakeRetriever();
        fake.EnqueueResponse(Array.Empty<ScoredChunk>());
        await fake.RetrieveAsync("q", new RetrievalOptions { TopK = 1 });

        fake.Reset();

        Assert.That(fake.ReceivedQueries, Is.Empty);
        Assert.That(fake.CallCount, Is.EqualTo(0));
        Assert.That(
            async () => await fake.RetrieveAsync("q", new RetrievalOptions { TopK = 1 }),
            Throws.TypeOf<InvalidOperationException>(),
            "Queue should be empty after reset");
    }

    [Test]
    public void FakeResponses_Retriever_WithDefault()
    {
        var chunks = new ScoredChunk[] { new(MakeChunk("doc", 0), 0.9) };
        var fake = FakeResponses.Retriever(chunks);

        Assert.That(fake.DefaultResponse, Is.EqualTo(chunks));
    }

    [Test]
    public void FakeResponses_Retriever_Empty()
    {
        var fake = FakeResponses.Retriever();

        Assert.That(fake.DefaultResponse, Is.Empty);
    }

    [Test]
    public async Task DI_AddFakeRetriever_RegistersAndReturnsInstance()
    {
        var services = new ServiceCollection();
        var fake = services.AddFakeRetriever();
        fake.DefaultResponse = Array.Empty<ScoredChunk>();

        using var provider = services.BuildServiceProvider();
        var retriever = provider.GetRequiredService<IRetriever>();

        var result = await retriever.RetrieveAsync("q", new RetrievalOptions { TopK = 5 });

        Assert.That(result, Is.Empty);
        Assert.That(fake.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ReceivedQueries_CapturesFullRetrievalOptions()
    {
        var fake = new FakeRetriever { DefaultResponse = Array.Empty<ScoredChunk>() };
        var options = new RetrievalOptions
        {
            TopK = 4,
            MinScore = 0.25,
            MetadataEquals = new Dictionary<string, string> { ["tenant"] = "acme" },
            ProviderQuery = new DummyProviderQuery("provider")
        };

        await fake.RetrieveAsync("query", options);

        Assert.That(fake.ReceivedQueries, Has.Count.EqualTo(1));
        Assert.That(fake.ReceivedQueries[0].Options, Is.SameAs(options));
        Assert.That(fake.ReceivedQueries[0].Options.ProviderQuery, Is.TypeOf<DummyProviderQuery>());
    }
}
