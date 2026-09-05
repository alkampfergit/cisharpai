using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Models;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class FixedSizeChunkerTests
{
    [TestCase("", 4, 1, new string[0], new int[0])]
    [TestCase("abc", 4, 1, new[] { "abc" }, new[] { 0 })]
    [TestCase("abcd", 4, 1, new[] { "abcd" }, new[] { 0 })]
    [TestCase("abcdefg", 4, 1, new[] { "abcd", "defg" }, new[] { 0, 3 })]
    [TestCase("abcdefgh", 4, 1, new[] { "abcd", "defg", "gh" }, new[] { 0, 3, 6 })]
    [TestCase("abcdef", 3, 0, new[] { "abc", "def" }, new[] { 0, 3 })]
    [TestCase("abcde", 3, 2, new[] { "abc", "bcd", "cde" }, new[] { 0, 1, 2 })]
    [TestCase(" \r\n\t ", 3, 0, new[] { " \r\n", "\t " }, new[] { 0, 3 })]
    public void Chunk_PreservesExactSlicesAndPositions(
        string text, int size, int overlap, string[] expected, int[] offsets)
    {
        var chunker = new FixedSizeChunker(new() { ChunkSize = size, Overlap = overlap });
        var chunks = chunker.Chunk(new RagDocument("doc", text)).ToList();

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
    public void Chunk_CountsUnicodeScalarsButReportsUtf16Offsets()
    {
        const string text = "A😀B🚀C";
        var chunks = new FixedSizeChunker(new() { ChunkSize = 3, Overlap = 1 })
            .Chunk(new("unicode", text)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(chunks.Select(c => c.Text), Is.EqualTo(new[] { "A😀B", "B🚀C" }));
            Assert.That(chunks.Select(c => c.StartOffset), Is.EqualTo(new[] { 0, 3 }));
        });
    }

    [Test]
    public void Chunk_OneScalarDoesNotSplitSurrogatePairs()
    {
        var chunks = new FixedSizeChunker(new() { ChunkSize = 1, Overlap = 0 })
            .Chunk(new("doc", "😀🚀")).ToList();
        Assert.That(chunks.Select(c => c.Text), Is.EqualTo(new[] { "😀", "🚀" }));
        Assert.That(chunks.Select(c => c.StartOffset), Is.EqualTo(new[] { 0, 2 }));
    }

    [Test]
    public void Chunk_PreservesUnpairedSurrogatesWithoutLooping()
    {
        var chunks = new FixedSizeChunker(new() { ChunkSize = 1, Overlap = 0 })
            .Chunk(new("doc", "\ud800A\udc00")).ToList();
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
    public void Constructor_SnapshotsOptions()
    {
        var options = new FixedSizeChunkerOptions { ChunkSize = 2, Overlap = 0 };
        var chunker = new FixedSizeChunker(options);
        options.ChunkSize = 0;
        var result = chunker.Chunk(new("doc", "abcd"));
        Assert.That(result.Select(c => c.Text), Is.EqualTo(new[] { "ab", "cd" }));
    }

    [Test]
    public void Chunk_IsRepeatableAndIndependentAcrossEnumerations()
    {
        var chunker = new FixedSizeChunker(new() { ChunkSize = 2, Overlap = 1 });
        var sequence = chunker.Chunk(new("doc", "abcd"));
        using var first = sequence.GetEnumerator();
        using var second = sequence.GetEnumerator();
        Assert.That(first.MoveNext(), Is.True);
        Assert.That(first.MoveNext(), Is.True);
        Assert.That(second.MoveNext(), Is.True);
        Assert.That(first.Current.Index, Is.EqualTo(1));
        Assert.That(second.Current.Index, Is.Zero);
    }

    [Test]
    public void DefaultOptions_AreUsable()
    {
        var chunks = new FixedSizeChunker().Chunk(new("doc", new string('x', 1025))).ToList();
        Assert.That(chunks.Select(c => c.StartOffset), Is.EqualTo(new[] { 0, 896 }));
        Assert.That(chunks.Select(c => c.Text.Length), Is.EqualTo(new[] { 1024, 129 }));
    }

    [Test]
    public void Chunk_ValidatesDocumentAtCallTime()
    {
        var chunker = new FixedSizeChunker();
        Assert.Throws<ArgumentNullException>(() => chunker.Chunk(null!));
        Assert.Throws<ArgumentNullException>(() => chunker.Chunk(new("doc", null!)));
        Assert.Throws<ArgumentException>(() => chunker.Chunk(new(" ", "text")));
    }
}
