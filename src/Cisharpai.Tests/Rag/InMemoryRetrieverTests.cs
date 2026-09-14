using Cisharpai.Models;
using Cisharpai.Rag;
using Cisharpai.Rag.Models;
using Cisharpai.Testing;
using NSubstitute;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class InMemoryRetrieverTests
{
    private static TextChunk MakeChunk(string docId, int index, string text) =>
        new(docId, index, 0, text.Length, text);

    [Test]
    public async Task RetrieveAsync_ReturnsTopKByCosineSimilarity()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(new float[] { 1f, 0f, 0f }));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        retriever.Add(MakeChunk("doc", 0, "alpha"), new float[] { 1f, 0f, 0f });
        retriever.Add(MakeChunk("doc", 1, "beta"), new float[] { 0f, 1f, 0f });
        retriever.Add(MakeChunk("doc", 2, "gamma"), new float[] { 0.7f, 0.7f, 0f });

        var results = await retriever.RetrieveAsync("query", 2);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Chunk.Text, Is.EqualTo("alpha"));
        Assert.That(results[1].Chunk.Text, Is.EqualTo("gamma"));
        Assert.That(results[0].Score, Is.GreaterThan(results[1].Score));
    }

    [Test]
    public async Task RetrieveAsync_TopKLargerThanStore_ReturnsAll()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(new float[] { 1f, 0f }));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        retriever.Add(MakeChunk("doc", 0, "only"), new float[] { 1f, 0f });

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RetrieveAsync_EmptyStore_ReturnsEmpty()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };

        var retriever = new InMemoryRetriever(fakeEmbedding);

        var results = await retriever.RetrieveAsync("query", 5);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task RetrieveAsync_EmbeddingFailure_ReturnsEmpty()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.EmbeddingError("model not found"));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        retriever.Add(MakeChunk("doc", 0, "text"), new float[] { 1f });

        var results = await retriever.RetrieveAsync("query", 5);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void RetrieveAsync_NullQuery_Throws()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        Assert.That(
            async () => await retriever.RetrieveAsync(null!, 5),
            Throws.ArgumentNullException);
    }

    [Test]
    public void RetrieveAsync_ZeroTopK_Throws()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        Assert.That(
            async () => await retriever.RetrieveAsync("query", 0),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void RetrieveAsync_NegativeTopK_Throws()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        Assert.That(
            async () => await retriever.RetrieveAsync("query", -1),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Constructor_NullEmbeddingClient_Throws()
    {
        Assert.That(
            () => new InMemoryRetriever(null!),
            Throws.ArgumentNullException);
    }

    [Test]
    public void Add_NullChunk_Throws()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        Assert.That(
            () => retriever.Add(null!, new float[] { 1f }),
            Throws.ArgumentNullException);
    }

    [Test]
    public void Add_NullVector_Throws()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        Assert.That(
            () => retriever.Add(MakeChunk("doc", 0, "text"), null!),
            Throws.ArgumentNullException);
    }

    [Test]
    public async Task RetrieveAsync_PassesModelToEmbeddingClient()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(new float[] { 1f }));

        var retriever = new InMemoryRetriever(fakeEmbedding, model: "text-embedding-3-small");
        retriever.Add(MakeChunk("doc", 0, "text"), new float[] { 1f });

        await retriever.RetrieveAsync("hello", 1);

        Assert.That(fakeEmbedding.ReceivedRequests[0].Model, Is.EqualTo("text-embedding-3-small"));
    }

    [Test]
    public async Task RetrieveAsync_DeterministicRanking_HandCraftedVectors()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(new float[] { 0f, 1f }));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        retriever.Add(MakeChunk("doc", 0, "orthogonal"), new float[] { 1f, 0f });
        retriever.Add(MakeChunk("doc", 1, "parallel"), new float[] { 0f, 1f });
        retriever.Add(MakeChunk("doc", 2, "diagonal"), new float[] { 0.5f, 0.5f });

        var results = await retriever.RetrieveAsync("query", 3);

        Assert.That(results[0].Chunk.Text, Is.EqualTo("parallel"));
        Assert.That(results[0].Score, Is.EqualTo(1.0f).Within(0.001f));
        Assert.That(results[1].Chunk.Text, Is.EqualTo("diagonal"));
        Assert.That(results[2].Chunk.Text, Is.EqualTo("orthogonal"));
        Assert.That(results[2].Score, Is.EqualTo(0.0f).Within(0.001f));
    }

    [Test]
    public void Count_ReflectsAddedItems()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        Assert.That(retriever.Count, Is.EqualTo(0));
        retriever.Add(MakeChunk("doc", 0, "text"), new float[] { 1f });
        Assert.That(retriever.Count, Is.EqualTo(1));
    }

    [Test]
    public void AddRange_AddsMultipleItems()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        retriever.AddRange(new[]
        {
            (MakeChunk("doc", 0, "a"), new float[] { 1f }),
            (MakeChunk("doc", 1, "b"), new float[] { 0f })
        });

        Assert.That(retriever.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task RetrieveAsync_CancellationRespected()
    {
        var embeddingClient = Substitute.For<IEmbeddingClient>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        embeddingClient.GetEmbeddingsAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return FakeResponses.Embedding();
            });

        var retriever = new InMemoryRetriever(embeddingClient);
        retriever.Add(MakeChunk("doc", 0, "text"), new float[] { 1f });

        Assert.That(
            async () => await retriever.RetrieveAsync("query", 1, cts.Token),
            Throws.TypeOf<OperationCanceledException>());
    }
}
