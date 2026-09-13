using Cisharpai.Models;
using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Embeddings;
using Cisharpai.Rag.Models;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class SemanticChunkerTests
{
    private static FakeEmbeddingClient CreateFakeClient(params EmbeddingResponse[] responses)
    {
        var client = new FakeEmbeddingClient();
        foreach (var r in responses)
            client.EnqueueResponse(r);
        return client;
    }

    private static EmbeddingResponse MakeEmbeddingResponse(params float[][] vectors) =>
        new(vectors, null, "test-model", vectors.Length * 10);

    private static BulkEmbeddingProcessor CreateProcessor(FakeEmbeddingClient client) =>
        new BulkEmbeddingProcessor(client, new BulkEmbeddingOptions
        {
            MaxBatchItems = 100,
            InputType = EmbeddingInputType.Document
        });

    private static async Task<List<TextChunk>> Collect(IAsyncEnumerable<TextChunk> source)
    {
        var results = new List<TextChunk>();
        await foreach (var chunk in source) results.Add(chunk);
        return results;
    }

    [Test]
    public async Task PercentileMode_PlacesBoundaryAtSimilarityDrop()
    {
        // Three sentences: S1 and S2 are similar, S3 is very different.
        // Vectors: [1,0], [0.95,0.05], [0,1] → sim(0,1)≈0.998, sim(1,2)≈0.05
        // Percentile 50 should place a boundary between S2 and S3.
        var text = "First sentence. Second sentence. Third totally different.";
        var vectors = new[]
        {
            new[] { 1f, 0f },
            new[] { 0.95f, 0.05f },
            new[] { 0f, 1f }
        };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Percentile,
            BreakPercentile = 50f,
            MaxChunkCharacters = 10000,
            MaxChunkSentences = 100
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(2));
        Assert.That(chunks[0].Text, Does.Contain("First"));
        Assert.That(chunks[0].Text, Does.Contain("Second"));
        Assert.That(chunks[1].Text, Does.Contain("Third"));
    }

    [Test]
    public async Task AbsoluteMode_PlacesBoundaryBelowThreshold()
    {
        var text = "Alpha here. Beta here. Gamma different.";
        var vectors = new[]
        {
            new[] { 1f, 0f },
            new[] { 0.9f, 0.1f },
            new[] { 0f, 1f }
        };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Absolute,
            AbsoluteThreshold = 0.5f,
            MaxChunkCharacters = 10000,
            MaxChunkSentences = 100
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(2));
        Assert.That(chunks[0].Text, Does.Contain("Alpha"));
        Assert.That(chunks[0].Text, Does.Contain("Beta"));
        Assert.That(chunks[1].Text, Does.Contain("Gamma"));
    }

    [Test]
    public async Task MaxChunkCharacters_ForcesBackstopCut()
    {
        // All sentences are very similar — no semantic boundary.
        // But the character limit forces a cut.
        var text = "Short one. Short two. Short three. Short four.";
        var vectors = new[]
        {
            new[] { 1f, 0f },
            new[] { 0.99f, 0.01f },
            new[] { 0.98f, 0.02f },
            new[] { 0.97f, 0.03f }
        };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Percentile,
            BreakPercentile = 5f,
            MaxChunkCharacters = 25,
            MaxChunkSentences = 100
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(chunks.All(c => c.Text.Length <= 30), Is.True,
            "All chunks should respect the character backstop");
    }

    [Test]
    public async Task MaxChunkSentences_ForcesBackstopCut()
    {
        // All identical vectors → similarity = 1.0 everywhere → no semantic boundary.
        // MaxChunkSentences = 3 forces a cut after every 3 sentences → 2 chunks from 6.
        var text = "One. Two. Three. Four. Five. Six.";
        var v = new[] { 1f, 0f };
        var vectors = new[] { v, v, v, v, v, v };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Absolute,
            AbsoluteThreshold = 0.1f,
            MaxChunkCharacters = 100000,
            MaxChunkSentences = 3
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SingleSentence_ReturnsSingleChunk()
    {
        var text = "Just one sentence here.";
        var vectors = new[] { new[] { 1f, 0f } };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor);

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(1));
        Assert.That(chunks[0].Text, Is.EqualTo("Just one sentence here."));
        Assert.That(chunks[0].Index, Is.EqualTo(0));
    }

    [Test]
    public async Task EmptyDocument_YieldsNoChunks()
    {
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor);

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", "")));

        Assert.That(chunks, Is.Empty);
    }

    [Test]
    public async Task WhitespaceOnlyDocument_YieldsNoChunks()
    {
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor);

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", "   \n\t  ")));

        Assert.That(chunks, Is.Empty);
    }

    [Test]
    public async Task ChunkOffsetsSpanSourceDocument()
    {
        var text = "First sentence. Second sentence. Third totally different.";
        var vectors = new[]
        {
            new[] { 1f, 0f },
            new[] { 0.95f, 0.05f },
            new[] { 0f, 1f }
        };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Absolute,
            AbsoluteThreshold = 0.5f,
            MaxChunkCharacters = 10000,
            MaxChunkSentences = 100
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.Multiple(() =>
        {
            Assert.That(chunks[0].StartOffset, Is.EqualTo(0));
            Assert.That(chunks[0].EndOffset, Is.LessThanOrEqualTo(text.Length));
            Assert.That(chunks[0].EndOffset, Is.GreaterThan(0));
            Assert.That(chunks.Last().EndOffset, Is.LessThanOrEqualTo(text.Length));
            Assert.That(chunks.All(c => c.DocumentId == "doc"), Is.True);
            Assert.That(chunks.Select(c => c.Index), Is.EqualTo(Enumerable.Range(0, chunks.Count)));
        });
    }

    [Test]
    public async Task CancellationIsHonoured()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var text = "First sentence. Second sentence. Third sentence.";
        var vectors = new[]
        {
            new[] { 1f, 0f },
            new[] { 0.9f, 0.1f },
            new[] { 0f, 1f }
        };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor);

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Collect(chunker.ChunkAsync(new RagDocument("doc", text), cts.Token)));
    }

    [Test]
    public void ValidatesDocumentAtCallTime()
    {
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor);

        Assert.Throws<ArgumentNullException>(() => chunker.ChunkAsync(null!));
        Assert.Throws<ArgumentNullException>(() => chunker.ChunkAsync(new RagDocument("doc", null!)));
        Assert.Throws<ArgumentException>(() => chunker.ChunkAsync(new RagDocument(" ", "text")));
    }

    [Test]
    public void Constructor_RejectsNullEmbeddingProcessor()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SemanticChunker(null!));
    }

    [Test]
    public void Options_RejectsInvalidMaxChunkCharacters()
    {
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SemanticChunker(processor, new SemanticChunkerOptions { MaxChunkCharacters = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SemanticChunker(processor, new SemanticChunkerOptions { MaxChunkCharacters = -1 }));
    }

    [Test]
    public void Options_RejectsInvalidMaxChunkSentences()
    {
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SemanticChunker(processor, new SemanticChunkerOptions { MaxChunkSentences = 0 }));
    }

    [Test]
    public void Options_RejectsInvalidBreakPercentile()
    {
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SemanticChunker(processor, new SemanticChunkerOptions
            {
                Strategy = SemanticThresholdStrategy.Percentile,
                BreakPercentile = 0f
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SemanticChunker(processor, new SemanticChunkerOptions
            {
                Strategy = SemanticThresholdStrategy.Percentile,
                BreakPercentile = 101f
            }));
    }

    [Test]
    public async Task UsesCustomSentenceSplitter()
    {
        var text = "Hello world|Goodbye world";
        var splitter = new PipeSplitter();
        var vectors = new[]
        {
            new[] { 1f, 0f },
            new[] { 0f, 1f }
        };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Absolute,
            AbsoluteThreshold = 0.5f,
            MaxChunkCharacters = 10000,
            MaxChunkSentences = 100
        }, sentenceSplitter: splitter);

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(2));
        Assert.That(chunks[0].Text, Is.EqualTo("Hello world"));
        Assert.That(chunks[1].Text, Is.EqualTo("Goodbye world"));
    }

    [Test]
    public async Task EmbeddingUsesSearchDocumentInputType()
    {
        var text = "First sentence. Second sentence.";
        var vectors = new[]
        {
            new[] { 1f, 0f },
            new[] { 0.9f, 0.1f }
        };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = new BulkEmbeddingProcessor(client, new BulkEmbeddingOptions
        {
            MaxBatchItems = 100,
            InputType = EmbeddingInputType.Document
        });
        var chunker = new SemanticChunker(processor);

        await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(client.ReceivedRequests, Has.Count.EqualTo(1));
        Assert.That(client.ReceivedRequests[0].InputType, Is.EqualTo(EmbeddingInputType.Document));
    }

    [Test]
    public async Task FourSentences_PercentileMode_TwoDistinctGroups()
    {
        // Sentences 0,1 similar; 2,3 similar; large gap between 1 and 2.
        // sim(0,1) ≈ 0.999, sim(1,2) ≈ 0, sim(2,3) ≈ 0.999
        // sorted sims: [0, 0.999, 0.999]. Percentile 25 → threshold ≈ 0.5
        // Only sim(1,2)=0 is ≤ 0.5, so one boundary between S1 and S2 → 2 chunks.
        var text = "Alpha topic. Alpha related. Beta topic. Beta related.";
        var vectors = new[]
        {
            new[] { 1f, 0f },
            new[] { 0.999f, 0.001f },
            new[] { 0f, 1f },
            new[] { 0.001f, 0.999f }
        };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Percentile,
            BreakPercentile = 25f,
            MaxChunkCharacters = 10000,
            MaxChunkSentences = 100
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(2));
        Assert.That(chunks[0].Text, Does.Contain("Alpha topic"));
        Assert.That(chunks[0].Text, Does.Contain("Alpha related"));
        Assert.That(chunks[1].Text, Does.Contain("Beta topic"));
        Assert.That(chunks[1].Text, Does.Contain("Beta related"));
    }

    [Test]
    public async Task MaxChunkCharacters_InAbsoluteMode_ForcesBackstopCut()
    {
        // All very similar, but character limit forces a cut.
        var text = "A sentence. B sentence. C sentence.";
        var vectors = new[]
        {
            new[] { 1f, 0f },
            new[] { 0.99f, 0.01f },
            new[] { 0.98f, 0.02f }
        };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Absolute,
            AbsoluteThreshold = 0.1f,
            MaxChunkCharacters = 25,
            MaxChunkSentences = 100
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2),
            "Character backstop should force at least one cut");
    }

    [Test]
    public async Task Options_AreSnapshotted()
    {
        var text = "First sentence. Second sentence.";
        var vectors = new[]
        {
            new[] { 1f, 0f },
            new[] { 0f, 1f }
        };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var options = new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Absolute,
            AbsoluteThreshold = 0.5f,
            MaxChunkCharacters = 10000,
            MaxChunkSentences = 100
        };
        var chunker = new SemanticChunker(processor, options);
        options.AbsoluteThreshold = 0.0f;

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(2),
            "Threshold should be snapshotted at 0.5, not mutated to 0.0");
    }

    [Test]
    public async Task EmbeddingFailure_ThrowsInvalidOperationException()
    {
        var text = "First sentence. Second sentence.";
        var client = CreateFakeClient(EmbeddingResponse.Error("batch failed"));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Collect(chunker.ChunkAsync(new RagDocument("doc", text))));
        Assert.That(ex!.Message, Does.Contain("batch failed"));
    }

    private sealed class PipeSplitter : ISentenceSplitter
    {
        public IReadOnlyList<string> Split(string text) =>
            text.Split('|', StringSplitOptions.RemoveEmptyEntries);
    }
}
