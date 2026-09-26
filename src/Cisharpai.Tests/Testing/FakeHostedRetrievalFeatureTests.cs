using Cisharpai.Rag.Models;
using Cisharpai.Rag.Packing;
using Cisharpai.Rag;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Testing;

public sealed class FakeHostedRetrievalFeatureTests
{
    private static ScoredChunk CreateChunk(string docId, double score) =>
        new(new TextChunk(docId, 0, 0, 5, "hello"), score);

    [Test]
    public void ForStore_RegisteredStore_ReturnsSameRetriever()
    {
        var fake = new FakeHostedRetrievalFeature();
        var retriever = fake.AddStore("vs_1");

        var result = fake.ForStore("vs_1");
        Assert.That(result, Is.SameAs(retriever));
    }

    [Test]
    public void ForStore_UnregisteredStore_ReturnsEmptyDefaultRetriever()
    {
        var fake = new FakeHostedRetrievalFeature();
        var retriever = fake.ForStore("vs_unknown");
        Assert.That(retriever, Is.Not.Null);
    }

    [Test]
    public async Task ForStore_UnregisteredStore_ReturnsEmptyResults()
    {
        var fake = new FakeHostedRetrievalFeature();
        var retriever = fake.ForStore("vs_unknown");
        var results = await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 5 });
        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task ForStore_RegisteredStore_ReturnsCannedResponses()
    {
        var fake = new FakeHostedRetrievalFeature();
        var fakeRetriever = fake.AddStore("vs_1");
        var expected = new List<ScoredChunk> { CreateChunk("doc-1", 0.9) };
        fakeRetriever.EnqueueResponse(expected);

        var retriever = fake.ForStore("vs_1");
        var results = await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 5 });

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Chunk.DocumentId, Is.EqualTo("doc-1"));
    }

    [Test]
    public void GetRetriever_RegisteredStore_ReturnsRetriever()
    {
        var fake = new FakeHostedRetrievalFeature();
        fake.AddStore("vs_1");

        Assert.That(fake.GetRetriever("vs_1"), Is.Not.Null);
    }

    [Test]
    public void GetRetriever_UnregisteredStore_ReturnsNull()
    {
        var fake = new FakeHostedRetrievalFeature();
        Assert.That(fake.GetRetriever("vs_nonexistent"), Is.Null);
    }

    [Test]
    public void StoreIds_TracksAccessed()
    {
        var fake = new FakeHostedRetrievalFeature();
        fake.AddStore("vs_1");
        fake.ForStore("vs_2");

        Assert.That(fake.StoreIds, Has.Count.EqualTo(2));
        Assert.That(fake.StoreIds, Does.Contain("vs_1"));
        Assert.That(fake.StoreIds, Does.Contain("vs_2"));
    }

    [Test]
    public async Task ForStore_CapturesQueries()
    {
        var fake = new FakeHostedRetrievalFeature();
        var fakeRetriever = fake.AddStore("vs_1");
        fakeRetriever.DefaultResponse = [];

        var retriever = fake.ForStore("vs_1");
        await retriever.RetrieveAsync("my query", new RetrievalOptions { TopK = 10 });

        Assert.That(fakeRetriever.ReceivedQueries, Has.Count.EqualTo(1));
        Assert.That(fakeRetriever.ReceivedQueries[0].Query, Is.EqualTo("my query"));
        Assert.That(fakeRetriever.ReceivedQueries[0].Options.TopK, Is.EqualTo(10));
    }

    [Test]
    public void Reset_ClearsAllStores()
    {
        var fake = new FakeHostedRetrievalFeature();
        fake.AddStore("vs_1");
        fake.ForStore("vs_2");

        fake.Reset();

        Assert.That(fake.StoreIds, Is.Empty);
    }

    [Test]
    public void AddStore_WithExistingRetriever_UsesProvided()
    {
        var fake = new FakeHostedRetrievalFeature();
        var custom = new FakeRetriever();
        fake.AddStore("vs_1", custom);

        Assert.That(fake.ForStore("vs_1"), Is.SameAs(custom));
    }

    [Test]
    public async Task TwoStores_IndependentResults()
    {
        var fake = new FakeHostedRetrievalFeature();

        var r1 = fake.AddStore("vs_1");
        r1.DefaultResponse = [CreateChunk("doc-from-1", 0.9)];

        var r2 = fake.AddStore("vs_2");
        r2.DefaultResponse = [CreateChunk("doc-from-2", 0.8)];

        var results1 = await fake.ForStore("vs_1").RetrieveAsync("q", new RetrievalOptions { TopK = 5 });
        var results2 = await fake.ForStore("vs_2").RetrieveAsync("q", new RetrievalOptions { TopK = 5 });

        Assert.Multiple(() =>
        {
            Assert.That(results1[0].Chunk.DocumentId, Is.EqualTo("doc-from-1"));
            Assert.That(results2[0].Chunk.DocumentId, Is.EqualTo("doc-from-2"));
        });
    }
}
