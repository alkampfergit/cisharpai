using Cisharpai.Rag;
using Cisharpai.Rag.Models;
using Cisharpai.Rag.Packing;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class RetrievalFilteringTests
{
    private sealed record DummyProviderQuery : IRetrievalQueryExtension;

    private static TextChunk MakeChunk(
        string docId,
        int index,
        string text,
        IReadOnlyDictionary<string, object?>? metadata = null) =>
        metadata is null
            ? new(docId, index, 0, text.Length, text)
            : new(docId, index, 0, text.Length, text, metadata);

    [Test]
    public void MatchesMetadata_CultureInvariant_DoubleValue()
    {
        var chunk = MakeChunk("doc", 0, "text",
            new Dictionary<string, object?> { ["price"] = 1.5d });

        var result = RetrievalFiltering.MatchesMetadata(
            chunk,
            new Dictionary<string, string> { ["price"] = "1.5" });

        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesMetadata_MissingKey_ReturnsFalse()
    {
        var chunk = MakeChunk("doc", 0, "text",
            new Dictionary<string, object?> { ["other"] = "val" });

        var result = RetrievalFiltering.MatchesMetadata(
            chunk,
            new Dictionary<string, string> { ["missing"] = "val" });

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesMetadata_MismatchedValue_ReturnsFalse()
    {
        var chunk = MakeChunk("doc", 0, "text",
            new Dictionary<string, object?> { ["key"] = "wrong" });

        var result = RetrievalFiltering.MatchesMetadata(
            chunk,
            new Dictionary<string, string> { ["key"] = "expected" });

        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesMetadata_NullMetadataValue_ReturnsFalse()
    {
        var chunk = MakeChunk("doc", 0, "text",
            new Dictionary<string, object?> { ["key"] = null });

        var result = RetrievalFiltering.MatchesMetadata(
            chunk,
            new Dictionary<string, string> { ["key"] = "expected" });

        Assert.That(result, Is.False);
    }

    [Test]
    public void ApplyPostFilters_MetadataAndMinScoreAndTopK()
    {
        var scored = new List<ScoredChunk>
        {
            new(MakeChunk("doc", 0, "a", new Dictionary<string, object?> { ["t"] = "x" }), 0.9),
            new(MakeChunk("doc", 1, "b", new Dictionary<string, object?> { ["t"] = "x" }), 0.5),
            new(MakeChunk("doc", 2, "c", new Dictionary<string, object?> { ["t"] = "y" }), 0.95),
        };

        var options = new RetrievalOptions
        {
            TopK = 1,
            MinScore = 0.6,
            MetadataEquals = new Dictionary<string, string> { ["t"] = "x" }
        };

        var result = RetrievalFiltering.ApplyPostFilters(scored, options, defaultTopK: 10);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Chunk.Text, Is.EqualTo("a"));
    }

    [Test]
    public void ApplyPostFilters_NullTopK_UsesDefault()
    {
        var scored = Enumerable.Range(0, 20)
            .Select(i => new ScoredChunk(MakeChunk("doc", i, $"chunk{i}"), 0.9 - i * 0.01))
            .ToList();

        var options = new RetrievalOptions();

        var result = RetrievalFiltering.ApplyPostFilters(scored, options, defaultTopK: 5);

        Assert.That(result, Has.Count.EqualTo(5));
    }

    [Test]
    public void ApplyPostFilters_OrdersByScoreDescending()
    {
        var scored = new List<ScoredChunk>
        {
            new(MakeChunk("doc", 0, "low"), 0.1),
            new(MakeChunk("doc", 1, "high"), 0.9),
            new(MakeChunk("doc", 2, "mid"), 0.5),
        };

        var result = RetrievalFiltering.ApplyPostFilters(scored, new RetrievalOptions { TopK = 3 }, 10);

        Assert.That(result[0].Chunk.Text, Is.EqualTo("high"));
        Assert.That(result[1].Chunk.Text, Is.EqualTo("mid"));
        Assert.That(result[2].Chunk.Text, Is.EqualTo("low"));
    }

    [Test]
    public void ThrowIfUnsupportedProviderQuery_NullQuery_DoesNotThrow()
    {
        var options = new RetrievalOptions();
        Assert.DoesNotThrow(() => RetrievalFiltering.ThrowIfUnsupportedProviderQuery(options, "TestRetriever"));
    }

    [Test]
    public void ThrowIfUnsupportedProviderQuery_WithQuery_Throws()
    {
        var options = new RetrievalOptions { ProviderQuery = new DummyProviderQuery() };

        var ex = Assert.Throws<ArgumentException>(
            () => RetrievalFiltering.ThrowIfUnsupportedProviderQuery(options, "TestRetriever"));

        Assert.That(ex!.Message, Does.Contain("TestRetriever"));
        Assert.That(ex.Message, Does.Contain(nameof(DummyProviderQuery)));
    }
}
