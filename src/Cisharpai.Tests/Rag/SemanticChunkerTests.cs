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

    private static void AssertVerbatimContract(IReadOnlyList<TextChunk> chunks, string sourceText)
    {
        foreach (var chunk in chunks)
            Assert.That(chunk.Text, Is.EqualTo(sourceText[chunk.StartOffset..chunk.EndOffset]),
                $"Chunk {chunk.Index} text must match source span (verbatim contract)");
    }

    private static void AssertContiguousCoverage(List<TextChunk> chunks, string sourceText)
    {
        if (chunks.Count == 0) return;
        for (var i = 1; i < chunks.Count; i++)
            Assert.That(chunks[i].StartOffset, Is.EqualTo(chunks[i - 1].EndOffset),
                $"Gap between chunk {i - 1} and chunk {i} — spans are not contiguous");
        var reconstructed = string.Concat(chunks.Select(c => c.Text));
        var covered = sourceText[chunks[0].StartOffset..chunks[^1].EndOffset];
        Assert.That(reconstructed, Is.EqualTo(covered), "Concatenated chunks must reconstruct source span");
    }

    [Test]
    public async Task PercentileMode_PlacesBoundaryAtSimilarityDrop()
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
            Strategy = SemanticThresholdStrategy.Percentile,
            BreakPercentile = 50f,
            MaxChunkCharacters = 10000,
            MaxChunkSentences = 100
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(2));
        Assert.That(chunks[0].Text, Is.EqualTo("First sentence. Second sentence. "));
        Assert.That(chunks[1].Text, Is.EqualTo("Third totally different."));
        AssertContiguousCoverage(chunks, text);
        AssertVerbatimContract(chunks, text);
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
        Assert.That(chunks[0].Text, Is.EqualTo("Alpha here. Beta here. "));
        Assert.That(chunks[1].Text, Is.EqualTo("Gamma different."));
        AssertContiguousCoverage(chunks, text);
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task MaxChunkCharacters_ForcesBackstopCut()
    {
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
        const int charLimit = 25;
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Percentile,
            BreakPercentile = 5f,
            MaxChunkCharacters = charLimit,
            MaxChunkSentences = 100
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(chunks.All(c => c.Text.Length <= charLimit), Is.True,
            $"All chunks must respect MaxChunkCharacters = {charLimit}");
        AssertContiguousCoverage(chunks, text);
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task MaxChunkSentences_ForcesBackstopCut()
    {
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
        Assert.That(chunks[0].Text, Is.EqualTo("One. Two. Three. "));
        Assert.That(chunks[1].Text, Is.EqualTo("Four. Five. Six."));
        AssertContiguousCoverage(chunks, text);
        AssertVerbatimContract(chunks, text);
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
        AssertVerbatimContract(chunks, text);
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
            Assert.That(chunks.Last().EndOffset, Is.EqualTo(text.Length));
            Assert.That(chunks.All(c => c.DocumentId == "doc"), Is.True);
            Assert.That(chunks.Select(c => c.Index), Is.EqualTo(Enumerable.Range(0, chunks.Count)));
        });
        AssertContiguousCoverage(chunks, text);
        AssertVerbatimContract(chunks, text);
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
    public void Cancellation_HonouredOnSingleSentenceFastPath()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var text = "Just one sentence.";
        var client = new FakeEmbeddingClient();
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
    public void Options_RejectsNaNBreakPercentile()
    {
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SemanticChunker(processor, new SemanticChunkerOptions
            {
                Strategy = SemanticThresholdStrategy.Percentile,
                BreakPercentile = float.NaN
            }));
    }

    [Test]
    public void Options_RejectsNaNAbsoluteThreshold()
    {
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SemanticChunker(processor, new SemanticChunkerOptions
            {
                Strategy = SemanticThresholdStrategy.Absolute,
                AbsoluteThreshold = float.NaN
            }));
    }

    [Test]
    public void Options_RejectsInfinityBreakPercentile()
    {
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SemanticChunker(processor, new SemanticChunkerOptions
            {
                Strategy = SemanticThresholdStrategy.Percentile,
                BreakPercentile = float.PositiveInfinity
            }));
    }

    [Test]
    public void Options_RejectsUnknownStrategy()
    {
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SemanticChunker(processor, new SemanticChunkerOptions
            {
                Strategy = (SemanticThresholdStrategy)999
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
        Assert.That(chunks[0].Text, Is.EqualTo("Hello world|"));
        Assert.That(chunks[1].Text, Is.EqualTo("Goodbye world"));
        AssertContiguousCoverage(chunks, text);
        AssertVerbatimContract(chunks, text);
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
        Assert.That(chunks[0].Text, Is.EqualTo("Alpha topic. Alpha related. "));
        Assert.That(chunks[1].Text, Is.EqualTo("Beta topic. Beta related."));
        AssertContiguousCoverage(chunks, text);
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task MaxChunkCharacters_InAbsoluteMode_ForcesBackstopCut()
    {
        var text = "A sentence. B sentence. C sentence.";
        var vectors = new[]
        {
            new[] { 1f, 0f },
            new[] { 0.99f, 0.01f },
            new[] { 0.98f, 0.02f }
        };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        const int charLimit = 25;
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Absolute,
            AbsoluteThreshold = 0.1f,
            MaxChunkCharacters = charLimit,
            MaxChunkSentences = 100
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2),
            "Character backstop should force at least one cut");
        Assert.That(chunks.All(c => c.Text.Length <= charLimit), Is.True,
            $"All chunks must respect MaxChunkCharacters = {charLimit}");
        AssertContiguousCoverage(chunks, text);
        AssertVerbatimContract(chunks, text);
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

    [Test]
    public async Task VerbatimContract_PreservesExactWhitespace()
    {
        var text = "First.  Second.";
        var vectors = new[]
        {
            new[] { 1f, 0f },
            new[] { 0.9f, 0.1f }
        };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Absolute,
            AbsoluteThreshold = 0.1f,
            MaxChunkCharacters = 10000,
            MaxChunkSentences = 100
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(1));
        Assert.That(chunks[0].Text, Is.EqualTo(text),
            "Multi-space whitespace must be preserved verbatim from source");
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public void OversizedSingleSentence_ThrowsInvalidOperationException()
    {
        var text = "This is a very long sentence that exceeds the character limit we set.";
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Absolute,
            AbsoluteThreshold = 0.5f,
            MaxChunkCharacters = 10,
            MaxChunkSentences = 100
        });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Collect(chunker.ChunkAsync(new RagDocument("doc", text))));
        Assert.That(ex!.Message, Does.Contain("MaxChunkCharacters"));
    }

    [Test]
    public void OversizedSentenceInMultiSentenceDocument_ThrowsBeforeEmbedding()
    {
        var text = "This sentence is way too long for the configured maximum. Short.";
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Percentile,
            BreakPercentile = 10f,
            MaxChunkCharacters = 10,
            MaxChunkSentences = 100
        });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Collect(chunker.ChunkAsync(new RagDocument("doc", text))));
        Assert.That(ex!.Message, Does.Contain("MaxChunkCharacters"));
        Assert.That(client.ReceivedRequests, Is.Empty,
            "Oversized sentence must be caught before any embedding call");
    }

    [Test]
    public void OversizedSentenceAfterBoundary_ThrowsBeforeEmbedding()
    {
        var text = "Short. This sentence is much too long for the configured maximum chunk character limit here.";
        var client = new FakeEmbeddingClient();
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Absolute,
            AbsoluteThreshold = 0.5f,
            MaxChunkCharacters = 50,
            MaxChunkSentences = 100
        });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Collect(chunker.ChunkAsync(new RagDocument("doc", text))));
        Assert.That(ex!.Message, Does.Contain("MaxChunkCharacters"));
        Assert.That(client.ReceivedRequests, Is.Empty,
            "Oversized sentence must be caught before any embedding call");
    }

    [Test]
    public async Task PercentileMode_UniformSimilarities_DoesNotProduceOneSentenceChunks()
    {
        var text = "One. Two. Three. Four. Five.";
        var v = new[] { 1f, 0f };
        var vectors = new[] { v, v, v, v, v };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Percentile,
            BreakPercentile = 10f,
            MaxChunkCharacters = 10000,
            MaxChunkSentences = 100
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(1),
            "Uniform similarities should produce no semantic boundaries — only backstop can cut");
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task PercentileMode_NearUniformSimilarities_TreatedAsUniform()
    {
        var text = "One. Two. Three. Four. Five.";
        var v1 = new[] { 1f, 0f };
        var v2 = new[] { 1f, 1e-7f };
        var vectors = new[] { v1, v2, v1, v2, v1 };
        var client = CreateFakeClient(MakeEmbeddingResponse(vectors));
        var processor = CreateProcessor(client);
        var chunker = new SemanticChunker(processor, new SemanticChunkerOptions
        {
            Strategy = SemanticThresholdStrategy.Percentile,
            BreakPercentile = 10f,
            MaxChunkCharacters = 10000,
            MaxChunkSentences = 100
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(1),
            "Near-uniform similarities (spread < epsilon) should produce no semantic boundaries");
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task SeparatorsAttachedToPrecedingChunk_ContiguousCoverage()
    {
        var text = "First.  Second.  Third different.";
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
        Assert.That(chunks[0].Text, Is.EqualTo("First.  Second.  "),
            "Separator (double space) must be attached to preceding chunk");
        Assert.That(chunks[1].Text, Is.EqualTo("Third different."));
        AssertContiguousCoverage(chunks, text);
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task Cancellation_HonouredDuringYieldAfterEmbedding()
    {
        var text = "First sentence. Second sentence. Third different.";
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

        using var cts = new CancellationTokenSource();
        var enumerator = chunker.ChunkAsync(new RagDocument("doc", text), cts.Token)
            .GetAsyncEnumerator(cts.Token);

        Assert.That(await enumerator.MoveNextAsync(), Is.True, "First chunk should be yielded");
        cts.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
    }

    private sealed class PipeSplitter : ISentenceSplitter
    {
        public IReadOnlyList<string> Split(string text) =>
            text.Split('|', StringSplitOptions.RemoveEmptyEntries);
    }
}
