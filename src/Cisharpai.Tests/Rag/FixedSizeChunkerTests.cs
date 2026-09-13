using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Models;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class FixedSizeChunkerTests
{
    private static readonly string[] UnicodeChunks3Overlap1 = ["A😀B", "B🚀C"];
    private static readonly int[] UnicodeOffsets3Overlap1 = [0, 3];
    private static readonly string[] SurrogatePairChunks = ["😀", "🚀"];
    private static readonly int[] SurrogatePairOffsets = [0, 2];
    private static readonly string[] TwoCharChunks = ["ab", "cd"];
    private static readonly int[] DefaultOffsets = [0, 896];
    private static readonly int[] DefaultLengths = [1024, 129];
    [TestCase("", 4, 1, new string[0], new int[0])]
    [TestCase("abc", 4, 1, new[] { "abc" }, new[] { 0 })]
    [TestCase("abcd", 4, 1, new[] { "abcd" }, new[] { 0 })]
    [TestCase("abcdefg", 4, 1, new[] { "abcd", "defg" }, new[] { 0, 3 })]
    [TestCase("abcdefgh", 4, 1, new[] { "abcd", "defg", "gh" }, new[] { 0, 3, 6 })]
    [TestCase("abcdef", 3, 0, new[] { "abc", "def" }, new[] { 0, 3 })]
    [TestCase("abcde", 3, 2, new[] { "abc", "bcd", "cde" }, new[] { 0, 1, 2 })]
    [TestCase(" \r\n\t ", 3, 0, new[] { " \r\n", "\t " }, new[] { 0, 3 })]
    public async Task ChunkAsync_PreservesExactSlicesAndPositions(
        string text, int size, int overlap, string[] expected, int[] offsets)
    {
        var chunker = new FixedSizeChunker(new() { ChunkSize = size, Overlap = overlap });
        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.Multiple(() =>
        {
            Assert.That(chunks.Select(c => c.Text), Is.EqualTo(expected));
            Assert.That(chunks.Select(c => c.StartOffset), Is.EqualTo(offsets));
            Assert.That(chunks.Select(c => c.Index), Is.EqualTo(Enumerable.Range(0, chunks.Count)));
            Assert.That(chunks.All(c => c.DocumentId == "doc"), Is.True);
            Assert.That(chunks.All(c => text.Substring(c.StartOffset, c.Text.Length) == c.Text), Is.True);
        });
    }

    [Test]
    public async Task ChunkAsync_CountsUnicodeScalarsButReportsUtf16Offsets()
    {
        const string text = "A😀B🚀C";
        var chunks = await Collect(new FixedSizeChunker(new() { ChunkSize = 3, Overlap = 1 })
            .ChunkAsync(new("unicode", text)));
        Assert.Multiple(() =>
        {
            Assert.That(chunks.Select(c => c.Text), Is.EqualTo(UnicodeChunks3Overlap1));
            Assert.That(chunks.Select(c => c.StartOffset), Is.EqualTo(UnicodeOffsets3Overlap1));
        });
    }

    [Test]
    public async Task ChunkAsync_OneScalarDoesNotSplitSurrogatePairs()
    {
        var chunks = await Collect(new FixedSizeChunker(new() { ChunkSize = 1, Overlap = 0 })
            .ChunkAsync(new("doc", "😀🚀")));
        Assert.That(chunks.Select(c => c.Text), Is.EqualTo(SurrogatePairChunks));
        Assert.That(chunks.Select(c => c.StartOffset), Is.EqualTo(SurrogatePairOffsets));
    }

    [Test]
    public async Task ChunkAsync_PreservesUnpairedSurrogatesWithoutLooping()
    {
        var chunks = await Collect(new FixedSizeChunker(new() { ChunkSize = 1, Overlap = 0 })
            .ChunkAsync(new("doc", "\ud800A\udc00")));
        Assert.That(string.Concat(chunks.Select(c => c.Text)), Is.EqualTo("\ud800A\udc00"));
        Assert.That(chunks, Has.Count.EqualTo(3));
    }

    [TestCase(0, 0)]
    [TestCase(-1, 0)]
    [TestCase(3, -1)]
    [TestCase(3, 3)]
    [TestCase(3, 4)]
    public void Constructor_RejectsInvalidOptions(int size, int overlap)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FixedSizeChunker(new() { ChunkSize = size, Overlap = overlap }));
    }

    [Test]
    public async Task Constructor_SnapshotsOptions()
    {
        var options = new FixedSizeChunkerOptions { ChunkSize = 2, Overlap = 0 };
        var chunker = new FixedSizeChunker(options);
        options.ChunkSize = 0;
        var result = await Collect(chunker.ChunkAsync(new("doc", "abcd")));
        Assert.That(result.Select(c => c.Text), Is.EqualTo(TwoCharChunks));
    }

    [Test]
    public async Task ChunkAsync_IsRepeatableAcrossEnumerations()
    {
        var chunker = new FixedSizeChunker(new() { ChunkSize = 2, Overlap = 1 });
        var first = await Collect(chunker.ChunkAsync(new("doc", "abcd")));
        var second = await Collect(chunker.ChunkAsync(new("doc", "abcd")));
        Assert.That(first.Select(c => c.Index), Is.EqualTo(second.Select(c => c.Index)));
        Assert.That(first.Select(c => c.Text), Is.EqualTo(second.Select(c => c.Text)));
    }

    [Test]
    public async Task DefaultOptions_AreUsable()
    {
        var chunks = await Collect(new FixedSizeChunker().ChunkAsync(new("doc", new string('x', 1025))));
        Assert.That(chunks.Select(c => c.StartOffset), Is.EqualTo(DefaultOffsets));
        Assert.That(chunks.Select(c => c.Text.Length), Is.EqualTo(DefaultLengths));
    }

    [Test]
    public void ChunkAsync_ValidatesDocumentAtCallTime()
    {
        var chunker = new FixedSizeChunker();
        Assert.Throws<ArgumentNullException>(() => chunker.ChunkAsync(null!));
        Assert.Throws<ArgumentNullException>(() => chunker.ChunkAsync(new("doc", null!)));
        Assert.Throws<ArgumentException>(() => chunker.ChunkAsync(new(" ", "text")));
    }

    [Test]
    public async Task ChunkAsync_EndOffsetEqualsStartPlusTextLength()
    {
        var chunker = new FixedSizeChunker(new() { ChunkSize = 3, Overlap = 1 });
        var chunks = await Collect(chunker.ChunkAsync(new("doc", "abcdefg")));
        Assert.That(chunks.All(c => c.EndOffset == c.StartOffset + c.Text.Length), Is.True);
    }

    [Test]
    public async Task ChunkAsync_EndOffsetCorrectWithSurrogatePairs()
    {
        const string text = "A😀B🚀C";
        var chunks = await Collect(new FixedSizeChunker(new() { ChunkSize = 3, Overlap = 1 })
            .ChunkAsync(new("doc", text)));
        Assert.Multiple(() =>
        {
            foreach (var chunk in chunks)
            {
                Assert.That(chunk.EndOffset, Is.EqualTo(chunk.StartOffset + chunk.Text.Length));
                Assert.That(text.Substring(chunk.StartOffset, chunk.EndOffset - chunk.StartOffset), Is.EqualTo(chunk.Text));
            }
        });
    }

    [Test]
    public async Task ChunkAsync_MetadataIsEmptyByDefault()
    {
        var chunks = await Collect(new FixedSizeChunker(new() { ChunkSize = 2, Overlap = 0 })
            .ChunkAsync(new("doc", "abcd")));
        Assert.Multiple(() =>
        {
            Assert.That(chunks.All(c => c.Metadata is not null), Is.True);
            Assert.That(chunks.All(c => c.Metadata.Count == 0), Is.True);
        });
    }

    [Test]
    public async Task ChunkAsync_RespectsCanellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var chunker = new FixedSizeChunker(new() { ChunkSize = 2, Overlap = 0 });
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Collect(chunker.ChunkAsync(new("doc", "abcd"), cts.Token)));
    }

    private static async Task<List<TextChunk>> Collect(IAsyncEnumerable<TextChunk> source)
    {
        var results = new List<TextChunk>();
        await foreach (var chunk in source) results.Add(chunk);
        return results;
    }
}
