using Cisharpai.Models;
using Cisharpai.Rag;
using Cisharpai.Rag.Models;
using Cisharpai.Rag.Packing;
using Cisharpai.Testing;
using NSubstitute;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class InMemoryRetrieverTests
{
    private sealed record DummyProviderQuery : IRetrievalQueryExtension;

    private static TextChunk MakeChunk(string docId, int index, string text) =>
        new(docId, index, 0, text.Length, text);

    private static TextChunk MakeChunk(
        string docId,
        int index,
        string text,
        IReadOnlyDictionary<string, object?> metadata) =>
        new(docId, index, 0, text.Length, text, metadata);

    [Test]
    public async Task RetrieveAsync_ReturnsTopKByCosineSimilarity()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(new float[] { 1f, 0f, 0f }));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        retriever.Add(MakeChunk("doc", 0, "alpha"), new float[] { 1f, 0f, 0f });
        retriever.Add(MakeChunk("doc", 1, "beta"), new float[] { 0f, 1f, 0f });
        retriever.Add(MakeChunk("doc", 2, "gamma"), new float[] { 0.7f, 0.7f, 0f });

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 2 });

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

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 10 });

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

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 5 });

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task RetrieveAsync_EmbeddingFailure_ReturnsEmpty()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.EmbeddingError("model not found"));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        retriever.Add(MakeChunk("doc", 0, "text"), new float[] { 1f });

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 5 });

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void RetrieveAsync_NullQuery_ThrowsSynchronously()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        Assert.Throws<ArgumentNullException>(() => retriever.RetrieveAsync(null!, new RetrievalOptions { TopK = 5 }));
    }

    [Test]
    public void RetrieveAsync_ZeroTopK_ThrowsSynchronously()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        Assert.Throws<ArgumentOutOfRangeException>(() => retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 0 }));
    }

    [Test]
    public void RetrieveAsync_NegativeTopK_ThrowsSynchronously()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        Assert.Throws<ArgumentOutOfRangeException>(() => retriever.RetrieveAsync("query", new RetrievalOptions { TopK = -1 }));
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

        await retriever.RetrieveAsync("hello", new RetrievalOptions { TopK = 1 });

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

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 3 });

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
            async () => await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 1 }, cts.Token),
            Throws.TypeOf<OperationCanceledException>());
    }

    [Test]
    public async Task RetrieveAsync_PassesQueryInputType()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(new float[] { 1f }));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        retriever.Add(MakeChunk("doc", 0, "text"), new float[] { 1f });

        await retriever.RetrieveAsync("hello", new RetrievalOptions { TopK = 1 });

        Assert.That(fakeEmbedding.ReceivedRequests[0].InputType, Is.EqualTo(EmbeddingInputType.Query));
    }

    [Test]
    public async Task RetrieveAsync_SuccessWithEmptyEmbeddings_ReturnsEmpty()
    {
        var embeddingClient = Substitute.For<IEmbeddingClient>();
        var emptySuccess = new EmbeddingResponse(
            Embeddings: Array.Empty<float[]>(),
            Base64Embeddings: null,
            Model: "test",
            TotalTokens: 0,
            IsSuccess: true);

        embeddingClient.GetEmbeddingsAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(emptySuccess);

        var retriever = new InMemoryRetriever(embeddingClient);
        retriever.Add(MakeChunk("doc", 0, "text"), new float[] { 1f });

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 5 });

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void RetrieveAsync_CancelledToken_EmptyStore_ThrowsBeforeReturning()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(
            async () => await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 1 }, cts.Token),
            Throws.TypeOf<OperationCanceledException>());
    }

    [Test]
    public void RetrieveAsync_CancelledAfterEmbedding_Throws()
    {
        var embeddingClient = Substitute.For<IEmbeddingClient>();
        var cts = new CancellationTokenSource();

        embeddingClient.GetEmbeddingsAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                cts.Cancel();
                return FakeResponses.Embedding(new float[] { 1f });
            });

        var retriever = new InMemoryRetriever(embeddingClient);
        retriever.Add(MakeChunk("doc", 0, "text"), new float[] { 1f });

        Assert.That(
            async () => await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 1 }, cts.Token),
            Throws.TypeOf<OperationCanceledException>());
    }

    [Test]
    public async Task RetrieveAsync_NullQueryVector_ReturnsEmpty()
    {
        var embeddingClient = Substitute.For<IEmbeddingClient>();
        var response = new EmbeddingResponse(
            Embeddings: new float[][] { null! },
            Base64Embeddings: null,
            Model: "test",
            TotalTokens: 0,
            IsSuccess: true);

        embeddingClient.GetEmbeddingsAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var retriever = new InMemoryRetriever(embeddingClient);
        retriever.Add(MakeChunk("doc", 0, "text"), new float[] { 1f });

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 5 });

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task RetrieveAsync_EmptyQueryVector_ReturnsEmpty()
    {
        var embeddingClient = Substitute.For<IEmbeddingClient>();
        var response = new EmbeddingResponse(
            Embeddings: new float[][] { Array.Empty<float>() },
            Base64Embeddings: null,
            Model: "test",
            TotalTokens: 0,
            IsSuccess: true);

        embeddingClient.GetEmbeddingsAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var retriever = new InMemoryRetriever(embeddingClient);
        retriever.Add(MakeChunk("doc", 0, "text"), new float[] { 1f });

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 5 });

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task RetrieveAsync_NonFiniteQueryVector_ReturnsEmpty()
    {
        var embeddingClient = Substitute.For<IEmbeddingClient>();
        var response = new EmbeddingResponse(
            Embeddings: new float[][] { new[] { 1f, float.NaN, 0f } },
            Base64Embeddings: null,
            Model: "test",
            TotalTokens: 0,
            IsSuccess: true);

        embeddingClient.GetEmbeddingsAsync(Arg.Any<EmbeddingRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var retriever = new InMemoryRetriever(embeddingClient);
        retriever.Add(MakeChunk("doc", 0, "text"), new float[] { 1f, 0f, 0f });

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 5 });

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void Add_SnapshotsVector_CallerMutationDoesNotAffectStore()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(new float[] { 1f, 0f }));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        var vector = new float[] { 1f, 0f };
        retriever.Add(MakeChunk("doc", 0, "text"), vector);

        vector[0] = 0f;
        vector[1] = 1f;

        // The stored vector should still be [1, 0], so "text" should rank first
        // against a query vector of [1, 0]
        var results = retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 1 }).GetAwaiter().GetResult();

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Score, Is.GreaterThan(0.99).Within(0.01));
    }

    [Test]
    public void AddRange_ChunkEmbedding_AddsItems()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        var items = new[]
        {
            new ChunkEmbedding(MakeChunk("doc", 0, "a"), new float[] { 1f }),
            new ChunkEmbedding(MakeChunk("doc", 1, "b"), new float[] { 0f })
        };
        retriever.AddRange(items);

        Assert.That(retriever.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task AddRange_ChunkEmbedding_RetrievesCorrectly()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(new float[] { 1f, 0f }));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        retriever.AddRange(new[]
        {
            new ChunkEmbedding(MakeChunk("doc", 0, "aligned"), new float[] { 1f, 0f }),
            new ChunkEmbedding(MakeChunk("doc", 1, "orthogonal"), new float[] { 0f, 1f })
        });

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 1 });

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Chunk.Text, Is.EqualTo("aligned"));
    }

    [Test]
    public async Task RetrieveAsync_MetadataEquals_AppliesAndSemantics()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(new float[] { 1f, 0f }));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        retriever.Add(
            MakeChunk("doc", 0, "acme-en", new Dictionary<string, object?> { ["tenant"] = "acme", ["lang"] = "en" }),
            new float[] { 1f, 0f });
        retriever.Add(
            MakeChunk("doc", 1, "acme-fr", new Dictionary<string, object?> { ["tenant"] = "acme", ["lang"] = "fr" }),
            new float[] { 1f, 0f });
        retriever.Add(
            MakeChunk("doc", 2, "other-en", new Dictionary<string, object?> { ["tenant"] = "other", ["lang"] = "en" }),
            new float[] { 1f, 0f });

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions
        {
            TopK = 10,
            MetadataEquals = new Dictionary<string, string>
            {
                ["tenant"] = "acme",
                ["lang"] = "en"
            }
        });

        Assert.That(results.Select(r => r.Chunk.Text), Is.EqualTo(new[] { "acme-en" }));
    }

    [Test]
    public async Task RetrieveAsync_MinScoreAndTopK_AppliesMinScoreBeforeTopK()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(new float[] { 1f, 0f }));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        retriever.Add(MakeChunk("doc", 0, "best"), new float[] { 1f, 0f });
        retriever.Add(MakeChunk("doc", 1, "good"), new float[] { 0.8f, 0.2f });
        retriever.Add(MakeChunk("doc", 2, "bad"), new float[] { 0f, 1f });

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions
        {
            TopK = 1,
            MinScore = 0.75
        });

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Chunk.Text, Is.EqualTo("best"));
    }

    [Test]
    public async Task RetrieveAsync_NullTopK_AppliesDefaultTopK()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(new float[] { 1f, 0f }));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        retriever.Add(MakeChunk("doc", 0, "first"), new float[] { 1f, 0f });
        retriever.Add(MakeChunk("doc", 1, "second"), new float[] { 0.8f, 0.2f });

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions
        {
            MinScore = 0.1
        });

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(InMemoryRetriever.DefaultTopK, Is.EqualTo(10));
    }

    [Test]
    public async Task RetrieveAsync_NullTopK_CapsAtDefaultTopK()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        var queryVector = new float[] { 1f };
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(queryVector));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        for (var i = 0; i < InMemoryRetriever.DefaultTopK + 5; i++)
            retriever.Add(MakeChunk("doc", i, $"chunk{i}"), new float[] { 1f });

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions());

        Assert.That(results, Has.Count.EqualTo(InMemoryRetriever.DefaultTopK));
    }

    [Test]
    public void RetrieveAsync_UnsupportedProviderQuery_ThrowsSynchronously()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        Assert.That(
            () => retriever.RetrieveAsync("query", new RetrievalOptions { TopK = 1, ProviderQuery = new DummyProviderQuery() }),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public async Task RetrieveAsync_MetadataEquals_CultureInvariant_NumericValue()
    {
        var fakeEmbedding = new FakeEmbeddingClient();
        fakeEmbedding.EnqueueResponse(FakeResponses.Embedding(new float[] { 1f, 0f }));

        var retriever = new InMemoryRetriever(fakeEmbedding);
        retriever.Add(
            MakeChunk("doc", 0, "with-decimal", new Dictionary<string, object?> { ["price"] = 1.5d }),
            new float[] { 1f, 0f });

        var results = await retriever.RetrieveAsync("query", new RetrievalOptions
        {
            TopK = 10,
            MetadataEquals = new Dictionary<string, string> { ["price"] = "1.5" }
        });

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Chunk.Text, Is.EqualTo("with-decimal"));
    }

    [Test]
    public void AddRange_TupleOverload_IsAtomic_AllOrNothingVisibility()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        var items = new[]
        {
            (MakeChunk("doc", 0, "a"), new float[] { 1f }),
            (MakeChunk("doc", 1, "b"), new float[] { 0f }),
            (MakeChunk("doc", 2, "c"), new float[] { 0.5f })
        };
        retriever.AddRange(items);

        Assert.That(retriever.Count, Is.EqualTo(3));
    }

    [Test]
    public void AddRange_ChunkEmbeddingOverload_IsAtomic()
    {
        var fakeEmbedding = new FakeEmbeddingClient
        {
            DefaultResponse = FakeResponses.Embedding()
        };
        var retriever = new InMemoryRetriever(fakeEmbedding);

        var items = new[]
        {
            new ChunkEmbedding(MakeChunk("doc", 0, "a"), new float[] { 1f }),
            new ChunkEmbedding(MakeChunk("doc", 1, "b"), new float[] { 0f })
        };
        retriever.AddRange(items);

        Assert.That(retriever.Count, Is.EqualTo(2));
    }
}
