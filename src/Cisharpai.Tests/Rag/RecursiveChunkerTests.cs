using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Models;
using Cisharpai.Rag.Tokenization;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class RecursiveChunkerTests
{
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

    private static void AssertFullCoverageWithOverlap(List<TextChunk> chunks, string sourceText)
    {
        if (chunks.Count == 0)
        {
            Assert.That(sourceText, Has.Length.EqualTo(0));
            return;
        }
        Assert.That(chunks[0].StartOffset, Is.EqualTo(0), "First chunk must start at offset 0");
        Assert.That(chunks[^1].EndOffset, Is.EqualTo(sourceText.Length), "Last chunk must end at document length");
        var covered = new bool[sourceText.Length];
        foreach (var c in chunks)
            for (var j = c.StartOffset; j < c.EndOffset; j++)
                covered[j] = true;
        for (var j = 0; j < sourceText.Length; j++)
            Assert.That(covered[j], Is.True, $"Offset {j} not covered by any chunk");
    }

    // --- Separator ladder ---

    [Test]
    public async Task SplitsOnParagraphs_WhenParagraphSeparatorPresent()
    {
        var text = "First paragraph.\n\nSecond paragraph.\n\nThird paragraph.";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 25,
            ChunkOverlap = 0
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(3));
        Assert.That(chunks[0].Text, Is.EqualTo("First paragraph.\n\n"));
        Assert.That(chunks[1].Text, Is.EqualTo("Second paragraph.\n\n"));
        Assert.That(chunks[2].Text, Is.EqualTo("Third paragraph."));
        AssertVerbatimContract(chunks, text);
        AssertContiguousCoverage(chunks, text);
    }

    [Test]
    public async Task FallsThroughToLineSeparator_WhenNoParagraphs()
    {
        var text = "Line one.\nLine two.\nLine three.";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 15,
            ChunkOverlap = 0
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(3));
        Assert.That(chunks[0].Text, Is.EqualTo("Line one.\n"));
        Assert.That(chunks[1].Text, Is.EqualTo("Line two.\n"));
        Assert.That(chunks[2].Text, Is.EqualTo("Line three."));
        AssertVerbatimContract(chunks, text);
        AssertContiguousCoverage(chunks, text);
    }

    [Test]
    public async Task FallsThroughToSentenceSeparator_WhenNoNewlines()
    {
        var text = "First sentence. Second sentence. Third sentence.";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 20,
            ChunkOverlap = 0
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        foreach (var c in chunks)
            Assert.That(c.Text, Has.Length.LessThanOrEqualTo(20));
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task FallsThroughToWordSeparator_WhenNoSentenceBoundaries()
    {
        var text = "one two three four five six";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 10,
            ChunkOverlap = 0
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(3));
        foreach (var c in chunks)
            Assert.That(c.Text, Has.Length.LessThanOrEqualTo(10));
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task HardCut_WhenNoSeparatorsApply()
    {
        var text = "abcdefghijklmnopqrstuvwxyz";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 10,
            ChunkOverlap = 0
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(3));
        Assert.That(chunks[0].Text, Is.EqualTo("abcdefghij"));
        Assert.That(chunks[1].Text, Is.EqualTo("klmnopqrst"));
        Assert.That(chunks[2].Text, Is.EqualTo("uvwxyz"));
        AssertVerbatimContract(chunks, text);
        AssertContiguousCoverage(chunks, text);
    }

    [Test]
    public async Task MixedLevels_FallsThroughLadder()
    {
        var text = "Paragraph one.\n\nLong sentence without newlines that exceeds the budget on its own for sure here.";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 30,
            ChunkOverlap = 0
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(chunks[0].Text, Does.StartWith("Paragraph one."));
        foreach (var c in chunks)
            Assert.That(c.Text, Has.Length.LessThanOrEqualTo(30));
        AssertVerbatimContract(chunks, text);
    }

    // --- Empty / small documents ---

    [Test]
    public async Task EmptyDocument_YieldsNoChunks()
    {
        var chunker = new RecursiveChunker();
        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", "")));
        Assert.That(chunks, Is.Empty);
    }

    [Test]
    public async Task SmallDocument_YieldsSingleChunk()
    {
        var text = "Short text.";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions { MaxChunkSize = 1000 });
        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(1));
        Assert.That(chunks[0].Text, Is.EqualTo(text));
        Assert.That(chunks[0].StartOffset, Is.EqualTo(0));
        Assert.That(chunks[0].EndOffset, Is.EqualTo(text.Length));
        Assert.That(chunks[0].Index, Is.EqualTo(0));
    }

    // --- Overlap ---

    [Test]
    public async Task Overlap_ProducesOverlappingChunks()
    {
        var text = "aaaa bbbb cccc dddd";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 10,
            ChunkOverlap = 3,
            Separators = [" ", ""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        for (var i = 1; i < chunks.Count; i++)
        {
            Assert.That(chunks[i].StartOffset, Is.LessThan(chunks[i - 1].EndOffset),
                $"Chunk {i} should overlap with chunk {i - 1}");
        }
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task ZeroOverlap_ProducesContiguousChunks()
    {
        var text = "aaaa bbbb cccc dddd";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 10,
            ChunkOverlap = 0,
            Separators = [" ", ""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        AssertContiguousCoverage(chunks, text);
        AssertVerbatimContract(chunks, text);
    }

    // --- Offset correctness ---

    [Test]
    public async Task OffsetsAreHonest_SourceSliceMatchesText()
    {
        var text = "First paragraph.\n\nSecond long paragraph that should be split. " +
                   "It has sentences. And more.\n\nThird.";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 30,
            ChunkOverlap = 0
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.Multiple(() =>
        {
            foreach (var c in chunks)
            {
                Assert.That(c.StartOffset, Is.GreaterThanOrEqualTo(0));
                Assert.That(c.EndOffset, Is.LessThanOrEqualTo(text.Length));
                Assert.That(c.EndOffset, Is.GreaterThan(c.StartOffset));
                Assert.That(c.Text, Is.EqualTo(text[c.StartOffset..c.EndOffset]));
                Assert.That(c.DocumentId, Is.EqualTo("doc"));
            }
            Assert.That(chunks.Select(c => c.Index), Is.EqualTo(Enumerable.Range(0, chunks.Count)));
        });
    }

    [Test]
    public async Task ContiguousWithoutOverlap_CoversFullDocument()
    {
        var text = "One. Two. Three. Four. Five. Six. Seven. Eight. Nine. Ten.";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 15,
            ChunkOverlap = 0
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks[0].StartOffset, Is.EqualTo(0));
        Assert.That(chunks[^1].EndOffset, Is.EqualTo(text.Length));
        AssertContiguousCoverage(chunks, text);
        AssertVerbatimContract(chunks, text);
    }

    // --- Surrogate pair safety ---

    [Test]
    public async Task HardCut_DoesNotSplitSurrogatePairs()
    {
        var text = "A😀B😀C😀D😀E";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 4,
            ChunkOverlap = 0
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        foreach (var c in chunks)
        {
            for (var j = 0; j < c.Text.Length; j++)
            {
                if (char.IsHighSurrogate(c.Text[j]))
                    Assert.That(j + 1 < c.Text.Length && char.IsLowSurrogate(c.Text[j + 1]), Is.True,
                        $"Chunk {c.Index} has unpaired high surrogate at position {j}");
            }
        }
        AssertVerbatimContract(chunks, text);
    }

    // --- Custom separators ---

    [Test]
    public async Task CustomSeparators_AreUsed()
    {
        var text = "a|b|c|d";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 3,
            ChunkOverlap = 0,
            Separators = ["|", ""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(3));
        Assert.That(chunks[0].Text, Is.EqualTo("a|"));
        Assert.That(chunks[1].Text, Is.EqualTo("b|"));
        Assert.That(chunks[2].Text, Is.EqualTo("c|d"));
        AssertVerbatimContract(chunks, text);
        AssertContiguousCoverage(chunks, text);
    }

    [Test]
    public async Task SeparatorNotInText_FallsThrough()
    {
        var text = "abcdefghij";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 4,
            ChunkOverlap = 0,
            Separators = ["|||", ""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(3));
        Assert.That(chunks[0].Text, Is.EqualTo("abcd"));
        Assert.That(chunks[1].Text, Is.EqualTo("efgh"));
        Assert.That(chunks[2].Text, Is.EqualTo("ij"));
        AssertVerbatimContract(chunks, text);
        AssertContiguousCoverage(chunks, text);
    }

    // --- Options validation & snapshot ---

    [Test]
    public void Options_RejectsInvalidMaxChunkSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecursiveChunker(new RecursiveChunkerOptions { MaxChunkSize = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecursiveChunker(new RecursiveChunkerOptions { MaxChunkSize = -1 }));
    }

    [Test]
    public void Options_RejectsInvalidOverlap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecursiveChunker(new RecursiveChunkerOptions { MaxChunkSize = 10, ChunkOverlap = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecursiveChunker(new RecursiveChunkerOptions { MaxChunkSize = 10, ChunkOverlap = 10 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecursiveChunker(new RecursiveChunkerOptions { MaxChunkSize = 10, ChunkOverlap = 11 }));
    }

    [Test]
    public void Options_RejectsEmptySeparators()
    {
        Assert.Throws<ArgumentException>(() =>
            new RecursiveChunker(new RecursiveChunkerOptions { Separators = [] }));
    }

    [Test]
    public void Options_RejectsNullSeparatorEntry()
    {
        Assert.Throws<ArgumentException>(() =>
            new RecursiveChunker(new RecursiveChunkerOptions { Separators = ["\n", null!, ""] }));
    }

    [Test]
    public async Task Options_AreSnapshotted()
    {
        var options = new RecursiveChunkerOptions { MaxChunkSize = 5, ChunkOverlap = 0 };
        var chunker = new RecursiveChunker(options);
        options.MaxChunkSize = 1000;

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", "abcdefghij")));

        Assert.That(chunks, Has.Count.GreaterThan(1),
            "MaxChunkSize should be snapshotted at 5, not mutated to 1000");
    }

    // --- Document validation ---

    [Test]
    public void ValidatesDocumentAtCallTime()
    {
        var chunker = new RecursiveChunker();
        Assert.Throws<ArgumentNullException>(() => chunker.ChunkAsync(null!));
        Assert.Throws<ArgumentNullException>(() => chunker.ChunkAsync(new RagDocument("doc", null!)));
        Assert.Throws<ArgumentException>(() => chunker.ChunkAsync(new RagDocument(" ", "text")));
    }

    // --- Cancellation ---

    [Test]
    public void CancellationIsHonoured()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var chunker = new RecursiveChunker();
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Collect(chunker.ChunkAsync(new RagDocument("doc", new string('x', 2000)), cts.Token)));
    }

    [Test]
    public async Task Cancellation_HonouredDuringYield()
    {
        var text = "aaaa bbbb cccc dddd eeee ffff";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 6,
            ChunkOverlap = 0
        });

        using var cts = new CancellationTokenSource();
        var enumerator = chunker.ChunkAsync(new RagDocument("doc", text), cts.Token)
            .GetAsyncEnumerator(cts.Token);

        Assert.That(await enumerator.MoveNextAsync(), Is.True, "First chunk should be yielded");
        cts.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
    }

    // --- Token mode ---

    [Test]
    public async Task TokenMode_UsesTokenCounter()
    {
        var counter = new FakeTokenCounter { DefaultCount = 5 };
        var text = "one two three four five six seven eight";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 5,
            ChunkOverlap = 0,
            TokenCounter = counter,
            Separators = [" ", ""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(counter.CallCount, Is.GreaterThan(0), "Token counter should have been called");
        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(1));
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task TokenMode_HardCut_UsesTokenSlicer()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var text = "This is a long text that has no separators and must be hard-cut by token count";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 5,
            ChunkOverlap = 0,
            TokenCounter = counter,
            TokenSlicerFromStart = counter.ToTokenSlicerFromStart(),
            Separators = ["|||NOT_FOUND|||", ""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        foreach (var c in chunks)
        {
            var tokenCount = counter.CountTokens(c.Text);
            Assert.That(tokenCount, Is.LessThanOrEqualTo(5),
                $"Chunk {c.Index} has {tokenCount} tokens, exceeds budget of 5");
        }
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task TokenMode_Overlap_UsesTokenSlicerFromEnd()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var text = "Hello world. This is a test. Another sentence here. And one more.";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 8,
            ChunkOverlap = 2,
            TokenCounter = counter,
            TokenSlicerFromStart = counter.ToTokenSlicerFromStart(),
            TokenSlicerFromEnd = counter.ToTokenSlicerFromEnd(),
            Separators = [". ", " ", ""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        for (var i = 1; i < chunks.Count; i++)
        {
            Assert.That(chunks[i].StartOffset, Is.LessThan(chunks[i - 1].EndOffset),
                $"Chunk {i} should overlap with chunk {i - 1}");
        }
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public void TokenMode_HardCut_ThrowsWithoutSlicer()
    {
        var counter = new FakeTokenCounter { DefaultCount = 100 };
        var text = "abcdefghijklmnopqrstuvwxyz";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 5,
            ChunkOverlap = 0,
            TokenCounter = counter,
            Separators = [""]
        });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Collect(chunker.ChunkAsync(new RagDocument("doc", text))));
        Assert.That(ex!.Message, Does.Contain("TokenSlicerFromStart"));
    }

    // --- Real tokenizer end-to-end ---

    [Test]
    public async Task TokenMode_RealTokenizer_ProducesCorrectlySizedChunks()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var text = string.Join(" ", Enumerable.Range(0, 200).Select(i => $"word{i}"));
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 50,
            ChunkOverlap = 0,
            TokenCounter = counter,
            TokenSlicerFromStart = counter.ToTokenSlicerFromStart(),
            Separators = [" ", ""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThan(1));
        foreach (var c in chunks)
        {
            var tokens = counter.CountTokens(c.Text);
            Assert.That(tokens, Is.LessThanOrEqualTo(50),
                $"Chunk {c.Index} ({tokens} tokens) exceeds budget");
        }
        AssertVerbatimContract(chunks, text);
    }

    // --- Merge behavior ---

    [Test]
    public async Task MergesSmallPieces_IntoLargerChunks()
    {
        var text = "a b c d e f g h";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 7,
            ChunkOverlap = 0,
            Separators = [" ", ""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.LessThan(8),
            "Small pieces should be merged rather than emitted individually");
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task AllChunksRespectMaxSize_CharacterMode()
    {
        var text = "The quick brown fox jumps over the lazy dog. " +
                   "Pack my box with five dozen liquor jugs. " +
                   "How vexingly quick daft zebras jump.";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 30,
            ChunkOverlap = 0
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        foreach (var c in chunks)
            Assert.That(c.Text, Has.Length.LessThanOrEqualTo(30),
                $"Chunk {c.Index} exceeds max size: '{c.Text}'");
    }

    // --- Metadata ---

    [Test]
    public async Task Chunks_HaveEmptyMetadata()
    {
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions { MaxChunkSize = 5, ChunkOverlap = 0 });
        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", "hello world")));

        Assert.That(chunks.All(c => c.Metadata is not null && c.Metadata.Count == 0), Is.True);
    }

    // --- TiktokenCounter new methods ---

    [Test]
    public void TiktokenCounter_GetIndexByTokenCount_ReturnsCorrectIndex()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var text = "Hello, world! How are you?";

        var index = counter.GetIndexByTokenCount(text, 2);

        Assert.That(index, Is.GreaterThan(0));
        Assert.That(index, Is.LessThanOrEqualTo(text.Length));
        var slice = text[..index];
        var sliceTokens = counter.CountTokens(slice);
        Assert.That(sliceTokens, Is.LessThanOrEqualTo(2));
    }

    [Test]
    public void TiktokenCounter_GetIndexByTokenCountFromEnd_ReturnsCorrectIndex()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var text = "Hello, world! How are you?";

        var index = counter.GetIndexByTokenCountFromEnd(text, 2);

        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        Assert.That(index, Is.LessThan(text.Length));
        var slice = text[index..];
        var sliceTokens = counter.CountTokens(slice);
        Assert.That(sliceTokens, Is.LessThanOrEqualTo(2));
    }

    [Test]
    public void TiktokenCounter_GetIndexByTokenCount_ZeroTokens_ReturnsZero()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var index = counter.GetIndexByTokenCount("Hello", 0);
        Assert.That(index, Is.EqualTo(0));
    }

    [Test]
    public void TiktokenCounter_GetIndexByTokenCount_AllTokensFit_ReturnsLength()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var text = "Hi";
        var totalTokens = counter.CountTokens(text);
        var index = counter.GetIndexByTokenCount(text, totalTokens + 10);
        Assert.That(index, Is.EqualTo(text.Length));
    }

    [Test]
    public void TiktokenCounter_GetIndexByTokenCount_ThrowsOnNull()
    {
        var counter = new TiktokenCounter("gpt-4o");
        Assert.Throws<ArgumentNullException>(() => counter.GetIndexByTokenCount(null!, 5));
        Assert.Throws<ArgumentNullException>(() => counter.GetIndexByTokenCountFromEnd(null!, 5));
    }

    [Test]
    public void TiktokenCounter_GetIndexByTokenCount_ThrowsOnNegativeCount()
    {
        var counter = new TiktokenCounter("gpt-4o");
        Assert.Throws<ArgumentOutOfRangeException>(() => counter.GetIndexByTokenCount("hello", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => counter.GetIndexByTokenCountFromEnd("hello", -1));
    }

    [Test]
    public void ToTokenSlicerFromStart_DelegatesToGetIndexByTokenCount()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var slicer = counter.ToTokenSlicerFromStart();
        var text = "Hello, world!";
        Assert.That(slicer(text, 2), Is.EqualTo(counter.GetIndexByTokenCount(text, 2)));
    }

    [Test]
    public void ToTokenSlicerFromEnd_DelegatesToGetIndexByTokenCountFromEnd()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var slicer = counter.ToTokenSlicerFromEnd();
        var text = "Hello, world!";
        Assert.That(slicer(text, 2), Is.EqualTo(counter.GetIndexByTokenCountFromEnd(text, 2)));
    }

    [Test]
    public void ToTokenSlicerFromStart_ThrowsOnNullCounter()
    {
        TiktokenCounter? counter = null;
        Assert.Throws<ArgumentNullException>(() => counter!.ToTokenSlicerFromStart());
    }

    [Test]
    public void ToTokenSlicerFromEnd_ThrowsOnNullCounter()
    {
        TiktokenCounter? counter = null;
        Assert.Throws<ArgumentNullException>(() => counter!.ToTokenSlicerFromEnd());
    }

    // --- Default options ---

    [Test]
    public async Task DefaultOptions_AreUsable()
    {
        var text = new string('x', 2000);
        var chunker = new RecursiveChunker();
        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        foreach (var c in chunks)
            Assert.That(c.Text, Has.Length.LessThanOrEqualTo(1024),
                $"Chunk {c.Index} exceeds default MaxChunkSize");
        AssertVerbatimContract(chunks, text);
        AssertFullCoverageWithOverlap(chunks, text);
    }

    // --- Separator without terminal empty string ---

    [Test]
    public async Task SeparatorListWithoutEmptyString_EmitsOversizedWhenNoSplitPossible()
    {
        var text = "abcdefghij";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 4,
            ChunkOverlap = 0,
            Separators = ["|||"]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.EqualTo(1));
        Assert.That(chunks[0].Text, Is.EqualTo(text));
    }

    // --- Overlap must not break max-size guarantee ---

    [Test]
    public async Task Overlap_NeverExceedsMaxChunkSize_CharacterMode()
    {
        var text = "aaaa bbbb cccc dddd eeee ffff";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 10,
            ChunkOverlap = 3,
            Separators = [" ", ""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        foreach (var c in chunks)
            Assert.That(c.Text, Has.Length.LessThanOrEqualTo(10),
                $"Chunk {c.Index} ({c.Text.Length} chars) exceeds MaxChunkSize");
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task Overlap_NeverExceedsMaxChunkSize_TokenMode()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var text = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"word{i}"));
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 20,
            ChunkOverlap = 5,
            TokenCounter = counter,
            TokenSlicerFromStart = counter.ToTokenSlicerFromStart(),
            TokenSlicerFromEnd = counter.ToTokenSlicerFromEnd(),
            Separators = [" ", ""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        foreach (var c in chunks)
        {
            var tokens = counter.CountTokens(c.Text);
            Assert.That(tokens, Is.LessThanOrEqualTo(20),
                $"Chunk {c.Index} ({tokens} tokens) exceeds MaxChunkSize");
        }
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task Overlap_CorpusGuarantee_AllChunksFitBudget()
    {
        var rng = new Random(42);
        var words = Enumerable.Range(0, 500).Select(_ =>
            new string((char)rng.Next('a', 'z' + 1), rng.Next(1, 12)));
        var text = string.Join(" ", words);

        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 40,
            ChunkOverlap = 10
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        foreach (var c in chunks)
            Assert.That(c.Text, Has.Length.LessThanOrEqualTo(40),
                $"Chunk {c.Index} ({c.Text.Length} chars) exceeds MaxChunkSize");
        AssertVerbatimContract(chunks, text);
    }

    // --- Emoji overlap: cap must use scalars, not code units (finding 1 counter-example) ---

    [Test]
    public async Task Overlap_EmojiText_NeverDropsSourceText()
    {
        var text = "😀😀😀😀";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 2,
            ChunkOverlap = 1
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        AssertVerbatimContract(chunks, text);
        AssertFullCoverageWithOverlap(chunks, text);
    }

    // --- De-overlapped chunks must reconstruct the full document ---

    [Test]
    public async Task DeOverlappedChunks_ReconstructDocument()
    {
        var text = "The quick brown fox jumps over the lazy dog and " +
                   "then runs around the park several times before resting.";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 30,
            ChunkOverlap = 8
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        AssertVerbatimContract(chunks, text);
        AssertFullCoverageWithOverlap(chunks, text);
    }

    // --- Hard-cut adjacent exactly-max chunks must maintain overlap (finding 2) ---

    [Test]
    public async Task HardCut_AdjacentExactlyMaxChunks_MaintainOverlap()
    {
        var text = "abcdefgh";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 4,
            ChunkOverlap = 1,
            Separators = [""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        Assert.That(chunks, Has.Count.GreaterThanOrEqualTo(2));
        for (var i = 1; i < chunks.Count; i++)
            Assert.That(chunks[i].StartOffset, Is.LessThan(chunks[i - 1].EndOffset),
                $"Chunk {i} should overlap with chunk {i - 1}");
        foreach (var c in chunks)
            Assert.That(c.Text, Has.Length.LessThanOrEqualTo(4),
                $"Chunk {c.Index} exceeds max size");
        AssertVerbatimContract(chunks, text);
        AssertFullCoverageWithOverlap(chunks, text);
    }

    // --- Oversized piece without terminal empty separator preserves prefix (finding 3) ---

    [Test]
    public async Task OversizedPiece_WithoutTerminalEmptySep_PreservesFullText()
    {
        var text = "ab cdefghijklmnop";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 4,
            ChunkOverlap = 1,
            Separators = [" "]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        AssertVerbatimContract(chunks, text);
        AssertFullCoverageWithOverlap(chunks, text);
        var oversized = chunks.FirstOrDefault(c => c.Text.Contains("cdefghijklmnop"));
        Assert.That(oversized, Is.Not.Null, "The oversized piece must be emitted intact");
    }

    // --- Finding 2: No empty chunks emitted ---

    [Test]
    public async Task NoEmptyChunks_WhenSeparatorAtEnd()
    {
        var text = "abc ";
        var chunker = new RecursiveChunker(new RecursiveChunkerOptions
        {
            MaxChunkSize = 2,
            ChunkOverlap = 0,
            Separators = [" ", ""]
        });

        var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

        foreach (var c in chunks)
            Assert.That(c.Text, Has.Length.GreaterThan(0),
                $"Chunk {c.Index} is empty");
        AssertVerbatimContract(chunks, text);
    }

    [Test]
    public async Task NoEmptyChunks_VariousInputs()
    {
        string[] inputs = ["a ", "ab\n", "x\n\n", "hello world ", "a b c "];

        foreach (var text in inputs)
        {
            var chunker = new RecursiveChunker(new RecursiveChunkerOptions
            {
                MaxChunkSize = 2,
                ChunkOverlap = 0
            });

            var chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", text)));

            foreach (var c in chunks)
                Assert.That(c.Text, Has.Length.GreaterThan(0),
                    $"Empty chunk in input '{text}' at index {c.Index}");
        }
    }

    // --- Finding 5: Token overlap without TokenSlicerFromEnd is rejected ---

    [Test]
    public void TokenMode_OverlapWithoutSlicerFromEnd_IsRejected()
    {
        var counter = new FakeTokenCounter { DefaultCount = 5 };
        var ex = Assert.Throws<ArgumentException>(() =>
            new RecursiveChunker(new RecursiveChunkerOptions
            {
                MaxChunkSize = 10,
                ChunkOverlap = 2,
                TokenCounter = counter,
                TokenSlicerFromStart = (_, _) => 5,
                TokenSlicerFromEnd = null
            }));
        Assert.That(ex!.Message, Does.Contain("TokenSlicerFromEnd"));
    }

    [Test]
    public void TokenMode_ZeroOverlapWithoutSlicerFromEnd_IsAccepted()
    {
        var counter = new FakeTokenCounter { DefaultCount = 5 };
        Assert.DoesNotThrow(() =>
            new RecursiveChunker(new RecursiveChunkerOptions
            {
                MaxChunkSize = 10,
                ChunkOverlap = 0,
                TokenCounter = counter,
                TokenSlicerFromEnd = null
            }));
    }
}
