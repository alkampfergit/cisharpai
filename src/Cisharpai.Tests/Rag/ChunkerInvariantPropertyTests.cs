using System.Globalization;
using System.Text;
using Cisharpai.Models;
using Cisharpai.Rag.Chunking;
using Cisharpai.Rag.Embeddings;
using Cisharpai.Rag.Models;
using Cisharpai.Rag.Tokenization;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag;

/// <summary>
/// Property-based invariant suite covering every <see cref="ITextChunker"/> implementation.
/// <para>
/// Each scenario runs a chunker over a deterministically generated corpus (fixed seeds, no
/// randomness that varies between runs) and asserts the four guarantees every chunker owes:
/// verbatim text, gap-free coverage, size bound, and sane offsets. A failure prints the seed,
/// the document kind, the escaped source text and the scenario options so it reproduces exactly.
/// </para>
/// <para>
/// Adding a chunker to this suite means adding one entry to <see cref="Scenarios"/>.
/// </para>
/// </summary>
[TestFixture]
public class ChunkerInvariantPropertyTests
{
    /// <summary>Fixed seeds — the corpus is identical on every run and on every target framework.</summary>
    private static readonly int[] Seeds = [1, 7, 42, 1337, 20260914];

    private const int MaxEscapedTextInFailureMessage = 600;

    // ---------------------------------------------------------------------
    // Scenario model
    // ---------------------------------------------------------------------

    /// <param name="Name">Human-readable scenario id, used in the NUnit test name and failure messages.</param>
    /// <param name="Factory">Builds a chunker for one generated document (Semantic needs per-document fake vectors).</param>
    /// <param name="Measure">Measures a source span in the chunker's own sizing unit.</param>
    /// <param name="MaxSize">The configured budget, expressed in the same unit as <paramref name="Measure"/>.</param>
    /// <param name="SizeBoundIsStrict">
    /// When true, every chunk must fit within <paramref name="MaxSize"/>. When false (specifically
    /// for <see cref="RecursiveChunker"/> without the terminal empty separator), individual chunks
    /// may exceed the budget but only if they are genuinely unsplittable — verified per-chunk by
    /// checking that no separator from <paramref name="Separators"/> appears in the chunk text.
    /// </param>
    /// <param name="Separators">
    /// The separator ladder for the chunker, when applicable. Used to verify that oversized chunks
    /// in non-strict scenarios are genuinely unsplittable atomic units rather than merge regressions.
    /// </param>
    /// <param name="ChunkOverlap">
    /// The overlap size for the chunker. Used to exclude the overlap prefix from the
    /// unsplittability check — pieces in the overlap region come from the previous chunk.
    /// </param>
    public sealed record ChunkerScenario(
        string Name,
        Func<GeneratedDocument, ITextChunker> Factory,
        Func<string, int, int, int> Measure,
        int MaxSize,
        bool SizeBoundIsStrict,
        IReadOnlyList<string>? Separators = null,
        int ChunkOverlap = 0)
    {
        public override string ToString() => Name;
    }

    public sealed record GeneratedDocument(string Kind, int Seed, string Text)
    {
        public override string ToString() => $"{Kind}#{Seed}";
    }

    // ---------------------------------------------------------------------
    // The property test
    // ---------------------------------------------------------------------

    [TestCaseSource(nameof(Scenarios))]
    public async Task EveryChunkSatisfiesTheChunkerInvariants(ChunkerScenario scenario)
    {
        var documentsExercised = 0;
        var documentsWithChunks = 0;

        foreach (var document in Corpus())
        {
            var chunker = scenario.Factory(document);

            List<TextChunk> chunks;
            try
            {
                chunks = await Collect(chunker.ChunkAsync(new RagDocument("doc", document.Text)));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("exceeds MaxChunkCharacters", StringComparison.Ordinal))
            {
                // Documented SemanticChunker mode: a single sentence cannot fit the budget, so the
                // chunker refuses rather than emitting an oversized chunk. Nothing to assert here.
                continue;
            }

            documentsExercised++;
            AssertInvariants(scenario, document, chunks);

            if (chunks.Count > 0)
                documentsWithChunks++;
        }

        Assert.That(documentsExercised, Is.GreaterThan(0),
            $"Scenario '{scenario.Name}' skipped every generated document — the suite asserted nothing.");
        Assert.That(documentsWithChunks, Is.GreaterThan(0),
            $"Scenario '{scenario.Name}' never produced chunks — only empty or whitespace-only " +
            $"documents passed, masking potential regressions that reject all real input.");
    }

    /// <summary>
    /// Both character-mode chunkers document the same sizing unit — Unicode scalar values, so a
    /// supplementary character counts as one however many UTF-16 code units it occupies. On a
    /// document containing none of the ladder separators, <see cref="RecursiveChunker"/> falls all
    /// the way through to its hard cut, which must then agree with <see cref="FixedSizeChunker"/>
    /// exactly. A scalar/UTF-16 confusion in either one shows up here as a boundary mismatch.
    /// </summary>
    [Test]
    public async Task CharacterModeChunkersAgreeOnTheScalarSizingUnit(
        [Values("no-whitespace", "all-surrogate-pairs")] string kind,
        [Values(1, 2, 3, 7, 16)] int size)
    {
        foreach (var seed in Seeds)
        {
            var document = new GeneratedDocument(kind, seed, Generate(kind, seed));

            var recursive = await Collect(new RecursiveChunker(
                new RecursiveChunkerOptions { MaxChunkSize = size, ChunkOverlap = 0 })
                .ChunkAsync(new RagDocument("doc", document.Text)));

            var fixedSize = await Collect(new FixedSizeChunker(
                new FixedSizeChunkerOptions { ChunkSize = size, Overlap = 0 })
                .ChunkAsync(new RagDocument("doc", document.Text)));

            Assert.That(
                recursive.Select(c => (c.StartOffset, c.EndOffset)),
                Is.EqualTo(fixedSize.Select(c => (c.StartOffset, c.EndOffset))),
                $"RecursiveChunker and FixedSizeChunker disagree on the scalar sizing unit. " +
                $"[kind={kind} seed={seed} size={size} text=\"{Escape(document.Text)}\"]");
        }
    }

    private static void AssertInvariants(
        ChunkerScenario scenario, GeneratedDocument document, List<TextChunk> chunks)
    {
        var text = document.Text;
        var context = Context(scenario, document);

        // --- Invariant 4: offsets are sane -------------------------------
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            Assert.That(chunk.Index, Is.EqualTo(i),
                $"Chunk indices must be contiguous from zero. {context}");
            Assert.That(chunk.StartOffset, Is.GreaterThanOrEqualTo(0),
                $"Chunk {i} has a negative StartOffset. {context}");
            Assert.That(chunk.EndOffset, Is.LessThanOrEqualTo(text.Length),
                $"Chunk {i} ends past the end of the document. {context}");
            Assert.That(chunk.EndOffset, Is.GreaterThan(chunk.StartOffset),
                $"Chunk {i} is empty ([{chunk.StartOffset}, {chunk.EndOffset})). {context}");
        }

        // --- Invariant 1: verbatim contract ------------------------------
        foreach (var chunk in chunks)
        {
            Assert.That(chunk.Text, Is.EqualTo(text[chunk.StartOffset..chunk.EndOffset]),
                $"Chunk {chunk.Index} text is not the verbatim source slice. {context}");
        }

        // --- Invariant 2: full coverage, no gaps -------------------------
        AssertCoverage(scenario, document, chunks, context);

        // --- Invariant 3: size bound -------------------------------------
        foreach (var chunk in chunks)
        {
            var size = scenario.Measure(text, chunk.StartOffset, chunk.EndOffset);
            if (size <= scenario.MaxSize)
                continue;

            if (!scenario.SizeBoundIsStrict)
            {
                AssertChunkIsUnsplittableAtomicUnit(scenario, chunk, context);
                continue;
            }

            Assert.Fail(
                $"Chunk {chunk.Index} measures {size} against a budget of {scenario.MaxSize} " +
                $"(span [{chunk.StartOffset}, {chunk.EndOffset})). {context}");
        }
    }

    private static void AssertCoverage(
        ChunkerScenario scenario, GeneratedDocument document, List<TextChunk> chunks, string context)
    {
        var text = document.Text;

        if (chunks.Count == 0)
        {
            if (text.Length == 0)
                return;

            // The only sanctioned way to emit nothing for a non-empty document is SemanticChunker
            // finding no sentences at all (a whitespace-only document). Everything else is data loss.
            Assert.That(scenario.Name, Does.StartWith("Semantic"),
                $"Chunker emitted no chunks for a non-empty document. {context}");
            Assert.That(new RegexSentenceSplitter().Split(text), Is.Empty,
                $"SemanticChunker emitted no chunks even though the splitter found sentences. {context}");
            return;
        }

        Assert.That(text, Is.Not.Empty,
            $"Chunker emitted {chunks.Count} chunk(s) for an empty document. {context}");

        for (var i = 1; i < chunks.Count; i++)
        {
            Assert.That(chunks[i].StartOffset, Is.GreaterThanOrEqualTo(chunks[i - 1].StartOffset),
                $"Chunks are not ordered by StartOffset at index {i}. {context}");
        }

        Assert.That(chunks[0].StartOffset, Is.Zero,
            $"First chunk starts at {chunks[0].StartOffset}, dropping the head of the document. {context}");
        Assert.That(chunks[^1].EndOffset, Is.EqualTo(text.Length),
            $"Last chunk ends at {chunks[^1].EndOffset} of {text.Length}, dropping the tail of the document. {context}");

        for (var i = 1; i < chunks.Count; i++)
        {
            Assert.That(chunks[i].StartOffset, Is.LessThanOrEqualTo(chunks[i - 1].EndOffset),
                $"Gap between chunk {i - 1} (ends {chunks[i - 1].EndOffset}) and chunk {i} " +
                $"(starts {chunks[i].StartOffset}) — source text was dropped. {context}");
        }
    }

    private static void AssertChunkIsUnsplittableAtomicUnit(
        ChunkerScenario scenario, TextChunk chunk, string context)
    {
        if (scenario.Separators is null)
            return;

        var chunkText = chunk.Text;

        // Chunks at index > 0 may have an overlap prefix from the previous chunk.
        // Pieces starting within that prefix are not part of the raw atom and should
        // not trigger a "splittable" failure. ChunkOverlap is in scalars; convert to
        // UTF-16 character positions since pieceStart uses char indices.
        var overlapPrefix = 0;
        if (chunk.Index > 0 && scenario.ChunkOverlap > 0)
        {
            var pos = 0;
            for (var s = 0; s < scenario.ChunkOverlap && pos < chunkText.Length; s++)
                pos += char.IsHighSurrogate(chunkText[pos]) && pos + 1 < chunkText.Length
                       && char.IsLowSurrogate(chunkText[pos + 1]) ? 2 : 1;
            overlapPrefix = pos;
        }

        foreach (var sep in scenario.Separators)
        {
            if (sep.Length == 0)
                continue;

            var contentStarts = new List<int> { 0 };
            var searchFrom = 0;
            while (searchFrom <= chunkText.Length - sep.Length)
            {
                var pos = chunkText.IndexOf(sep, searchFrom, StringComparison.Ordinal);
                if (pos < 0) break;
                contentStarts.Add(pos + sep.Length);
                searchFrom = pos + sep.Length;
            }

            if (contentStarts.Count <= 1)
                continue;

            for (var i = 0; i < contentStarts.Count; i++)
            {
                var pieceStart = contentStarts[i];
                if (pieceStart < overlapPrefix)
                    continue;

                var pieceEnd = i + 1 < contentStarts.Count ? contentStarts[i + 1] : chunkText.Length;
                var pieceLen = pieceEnd - pieceStart;
                var pieceSize = scenario.Measure(chunkText, pieceStart, pieceEnd);
                // Both the test's measure (scalars) AND the chunker's internal measure
                // (UTF-16 code units) must agree the piece fits, since the chunker uses
                // UTF-16 sizing to decide whether a piece exceeds the budget.
                if (pieceLen > 0 && pieceSize <= scenario.MaxSize && pieceLen <= scenario.MaxSize)
                {
                    Assert.Fail(
                        $"Chunk {chunk.Index} exceeds MaxSize ({scenario.MaxSize}) but separator " +
                        $"\"{Escape(sep)}\" splits it into pieces where at least one (size {pieceSize}) " +
                        $"fits within the budget — it is not genuinely unsplittable. {context}");
                }
            }
        }
    }

    private static string Context(ChunkerScenario scenario, GeneratedDocument document) =>
        $"[scenario={scenario.Name} kind={document.Kind} seed={document.Seed} " +
        $"length={document.Text.Length} text=\"{Escape(document.Text)}\"]";

    // ---------------------------------------------------------------------
    // Scenarios — one entry per (chunker, option set)
    // ---------------------------------------------------------------------

    private static IEnumerable<TestCaseData> Scenarios()
    {
        foreach (var scenario in BuildScenarios())
            yield return new TestCaseData(scenario).SetArgDisplayNames(scenario.Name);
    }

    private static IEnumerable<ChunkerScenario> BuildScenarios()
    {
        // --- FixedSizeChunker: sizes count Unicode scalar values ---------
        foreach (var (size, overlap) in new[]
                 {
                     (1, 0), (2, 0), (2, 1), (3, 1), (3, 2),
                     (7, 0), (7, 3), (7, 6), (16, 8), (64, 1), (257, 128)
                 })
        {
            var options = new FixedSizeChunkerOptions { ChunkSize = size, Overlap = overlap };
            yield return new ChunkerScenario(
                $"FixedSize_size{size}_overlap{overlap}",
                _ => new FixedSizeChunker(options),
                ScalarCount,
                size,
                SizeBoundIsStrict: true);
        }

        // --- RecursiveChunker, character mode, ladder ending in "" -------
        // The terminal empty separator is documented to guarantee the size bound.
        foreach (var (max, overlap) in new[]
                 {
                     (1, 0), (2, 0), (2, 1), (3, 1), (8, 0), (8, 4), (8, 7), (32, 8), (100, 99)
                 })
        {
            var options = new RecursiveChunkerOptions { MaxChunkSize = max, ChunkOverlap = overlap };
            yield return new ChunkerScenario(
                $"Recursive_default_max{max}_overlap{overlap}",
                _ => new RecursiveChunker(options),
                ScalarCount,
                max,
                SizeBoundIsStrict: true);
        }

        // Custom ladder that still terminates in "" — still a strict bound.
        foreach (var (max, overlap) in new[] { (8, 0), (16, 4) })
        {
            var options = new RecursiveChunkerOptions
            {
                MaxChunkSize = max,
                ChunkOverlap = overlap,
                Separators = ["\n", " ", ""]
            };
            yield return new ChunkerScenario(
                $"Recursive_shortLadder_max{max}_overlap{overlap}",
                _ => new RecursiveChunker(options),
                ScalarCount,
                max,
                SizeBoundIsStrict: true);
        }

        // --- RecursiveChunker, ladders WITHOUT the terminal "" -----------
        // Documented to emit an unsplittable atomic unit oversized, so the size bound is not
        // asserted; coverage, verbatim text and offset sanity still are.
        foreach (var (max, overlap, separators) in new (int, int, IReadOnlyList<string>)[]
                 {
                     (8, 0, ["\n\n", "\n", ". ", " "]),
                     (8, 4, ["\n\n", "\n", ". ", " "]),
                     (16, 0, [" "]),
                     (32, 8, ["\n\n", " "])
                 })
        {
            var options = new RecursiveChunkerOptions
            {
                MaxChunkSize = max,
                ChunkOverlap = overlap,
                Separators = separators
            };
            yield return new ChunkerScenario(
                $"Recursive_noHardCut_max{max}_overlap{overlap}_seps{separators.Count}",
                _ => new RecursiveChunker(options),
                ScalarCount,
                max,
                SizeBoundIsStrict: false,
                Separators: separators,
                ChunkOverlap: overlap);
        }

        // --- RecursiveChunker, token mode (local tokenizer only) ---------
        foreach (var (max, overlap) in new[] { (4, 2), (8, 0), (8, 4), (8, 7), (16, 12), (32, 8), (64, 32) })
        {
            var counter = new TiktokenCounter("gpt-4o");
            var options = new RecursiveChunkerOptions
            {
                MaxChunkSize = max,
                ChunkOverlap = overlap,
                TokenCounter = counter,
                TokenSlicerFromStart = counter.ToTokenSlicerFromStart(),
                TokenSlicerFromEnd = counter.ToTokenSlicerFromEnd()
            };
            yield return new ChunkerScenario(
                $"Recursive_tokenMode_max{max}_overlap{overlap}",
                _ => new RecursiveChunker(options),
                (text, start, end) => counter.CountTokens(text[start..end]),
                max,
                SizeBoundIsStrict: true);
        }

        // --- SemanticChunker: sizes count UTF-16 code units --------------
        foreach (var (strategy, maxChars, maxSentences) in new[]
                 {
                     (SemanticThresholdStrategy.Percentile, 32, 50),
                     (SemanticThresholdStrategy.Percentile, 120, 3),
                     (SemanticThresholdStrategy.Percentile, 4096, 1),
                     (SemanticThresholdStrategy.Absolute, 32, 50),
                     (SemanticThresholdStrategy.Absolute, 120, 2),
                     (SemanticThresholdStrategy.Absolute, 4096, 50)
                 })
        {
            var options = new SemanticChunkerOptions
            {
                Strategy = strategy,
                BreakPercentile = 25f,
                AbsoluteThreshold = 0.8f,
                MaxChunkCharacters = maxChars,
                MaxChunkSentences = maxSentences
            };
            yield return new ChunkerScenario(
                $"Semantic_{strategy}_chars{maxChars}_sentences{maxSentences}",
                doc => CreateSemanticChunker(doc, options),
                (_, start, end) => end - start,
                maxChars,
                SizeBoundIsStrict: true);
        }
    }

    private static SemanticChunker CreateSemanticChunker(GeneratedDocument document, SemanticChunkerOptions options)
    {
        var splitter = new RegexSentenceSplitter();
        var sentences = splitter.Split(document.Text);

        var client = new FakeEmbeddingClient();
        if (sentences.Count > 0)
            client.EnqueueResponse(new EmbeddingResponse(
                BuildDeterministicVectors(sentences.Count, document.Seed), null, "test-model", 0));

        var processor = new BulkEmbeddingProcessor(client, new BulkEmbeddingOptions
        {
            // One batch for the whole document so the enqueued response always lines up.
            MaxBatchItems = Math.Max(1, sentences.Count),
            InputType = EmbeddingInputType.Document
        });

        return new SemanticChunker(processor, options, splitter);
    }

    /// <summary>
    /// Hand-built unit vectors walking a circle: mostly small drifts (semantically similar
    /// neighbours) with occasional large jumps (topic shifts), so boundary placement is varied
    /// but fully reproducible from the seed.
    /// </summary>
    private static float[][] BuildDeterministicVectors(int count, int seed)
    {
        var rng = new XorShift(seed * 31 + 17);
        var vectors = new float[count][];
        var angle = 0d;
        for (var i = 0; i < count; i++)
        {
            angle += rng.Next(4) == 0 ? 1.2d : 0.05d;
            vectors[i] = [(float)Math.Cos(angle), (float)Math.Sin(angle)];
        }
        return vectors;
    }

    // ---------------------------------------------------------------------
    // Corpus generation — deterministic, seeded, non-BMP aware
    // ---------------------------------------------------------------------

    private static readonly string[] DocumentKinds =
    [
        "empty",
        "shorter-than-chunk",
        "ascii-prose",
        "emoji-dense",
        "cjk",
        "mixed-scripts",
        "whitespace-heavy",
        "no-whitespace",
        "all-surrogate-pairs",
        "leading-whitespace",
        "trailing-whitespace",
        "separators-only",
        "sparse-sentences",
        "repeated-runs"
    ];

    private static IEnumerable<GeneratedDocument> Corpus()
    {
        foreach (var kind in DocumentKinds)
        {
            foreach (var seed in Seeds)
                yield return new GeneratedDocument(kind, seed, Generate(kind, seed));
        }
    }

    private static readonly string[] Words =
    [
        "alpha", "beta", "gamma", "delta", "retrieval", "embedding", "chunk",
        "vector", "token", "corpus", "boundary", "overlap", "surrogate", "scalar"
    ];

    private static readonly string[] Emoji =
    [
        "\U0001F600", "\U0001F680", "\U0001F9EA", "\U0001F4DA", "\U0001F30D",
        "\U0001F525", "\U0001FAE0", "\U0001F468‍\U0001F4BB"
    ];

    private static readonly string[] Tiny =
    [
        "a", "ab", "ab ", " ", ".", "\U0001F600", "\U0001F600\U0001F680", "\n"
    ];

    private static string Generate(string kind, int seed)
    {
        var rng = new XorShift(seed);
        return kind switch
        {
            "empty" => string.Empty,
            "shorter-than-chunk" => Tiny[rng.Next(Tiny.Length)],
            "ascii-prose" => AsciiProse(ref rng),
            "emoji-dense" => EmojiDense(ref rng),
            "cjk" => Cjk(ref rng),
            "mixed-scripts" => MixedScripts(ref rng),
            "whitespace-heavy" => WhitespaceHeavy(ref rng),
            "no-whitespace" => NoWhitespace(ref rng),
            "all-surrogate-pairs" => AllSurrogatePairs(ref rng),
            "leading-whitespace" => WhitespaceRun(ref rng) + AsciiProse(ref rng),
            "trailing-whitespace" => AsciiProse(ref rng) + WhitespaceRun(ref rng),
            "separators-only" => SeparatorsOnly(ref rng),
            "sparse-sentences" => SparseSentences(ref rng),
            "repeated-runs" => RepeatedRuns(ref rng),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown document kind.")
        };
    }

    private static string AsciiProse(ref XorShift rng)
    {
        var sb = new StringBuilder();
        var sentences = 1 + rng.Next(10);
        for (var s = 0; s < sentences; s++)
        {
            var words = 1 + rng.Next(7);
            for (var w = 0; w < words; w++)
            {
                if (w > 0) sb.Append(' ');
                sb.Append(Words[rng.Next(Words.Length)]);
            }
            sb.Append(".!?"[rng.Next(3)]);
            if (s < sentences - 1)
                sb.Append(rng.Next(4) == 0 ? "\n\n" : " ");
        }
        return sb.ToString();
    }

    private static string EmojiDense(ref XorShift rng)
    {
        var sb = new StringBuilder();
        var count = 1 + rng.Next(60);
        for (var i = 0; i < count; i++)
        {
            sb.Append(Emoji[rng.Next(Emoji.Length)]);
            if (rng.Next(5) == 0) sb.Append(' ');
            if (rng.Next(11) == 0) sb.Append(". ");
        }
        return sb.ToString();
    }

    private static string Cjk(ref XorShift rng)
    {
        var sb = new StringBuilder();
        var count = 1 + rng.Next(80);
        for (var i = 0; i < count; i++)
        {
            sb.Append((char)(0x4E00 + rng.Next(0x1000)));
            if (rng.Next(9) == 0) sb.Append('。');
            if (rng.Next(13) == 0) sb.Append(' ');
        }
        return sb.ToString();
    }

    private static string MixedScripts(ref XorShift rng)
    {
        var sb = new StringBuilder();
        var count = 1 + rng.Next(90);
        for (var i = 0; i < count; i++)
        {
            switch (rng.Next(7))
            {
                case 0: sb.Append(Words[rng.Next(Words.Length)]); break;
                case 1: sb.Append(Emoji[rng.Next(Emoji.Length)]); break;
                case 2: sb.Append((char)(0x4E00 + rng.Next(0x1000))); break;
                case 3: sb.Append((char)(0x0410 + rng.Next(32))); break;   // Cyrillic
                case 4: sb.Append((char)(0x0620 + rng.Next(32))); break;   // Arabic
                case 5: sb.Append('e').Append((char)(0x0300 + rng.Next(16))); break; // combining mark
                default: sb.Append(" .\n"[rng.Next(3)]); break;
            }
        }
        return sb.ToString();
    }

    private static string WhitespaceHeavy(ref XorShift rng)
    {
        var sb = new StringBuilder();
        var count = 1 + rng.Next(120);
        for (var i = 0; i < count; i++)
        {
            if (rng.Next(8) == 0)
                sb.Append(Words[rng.Next(Words.Length)]).Append('.');
            else
                sb.Append(" \n\t"[rng.Next(3)]);
        }
        return sb.ToString();
    }

    private static string NoWhitespace(ref XorShift rng)
    {
        var sb = new StringBuilder();
        var count = 1 + rng.Next(150);
        for (var i = 0; i < count; i++)
            sb.Append((char)('a' + rng.Next(26)));
        return sb.ToString();
    }

    private static string AllSurrogatePairs(ref XorShift rng)
    {
        var sb = new StringBuilder();
        var count = 1 + rng.Next(50);
        for (var i = 0; i < count; i++)
            sb.Append(char.ConvertFromUtf32(0x10000 + rng.Next(0x1000)));
        return sb.ToString();
    }

    private static string WhitespaceRun(ref XorShift rng)
    {
        var sb = new StringBuilder();
        var count = 1 + rng.Next(12);
        for (var i = 0; i < count; i++)
            sb.Append(" \n\t"[rng.Next(3)]);
        return sb.ToString();
    }

    /// <summary>
    /// Short sentences separated by long whitespace runs. The separator run belongs to the
    /// preceding chunk, so the emitted span of a single sentence is far larger than the sentence
    /// itself — the exact shape that made the SemanticChunker size backstop under-count.
    /// </summary>
    private static string SparseSentences(ref XorShift rng)
    {
        var sb = new StringBuilder();
        var sentences = 1 + rng.Next(5);
        for (var s = 0; s < sentences; s++)
        {
            sb.Append(Words[rng.Next(Words.Length)]).Append('.');
            var gap = 1 + rng.Next(200);
            for (var i = 0; i < gap; i++)
                sb.Append(" \n\t"[rng.Next(3)]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Long runs of a repeated character or word. Sub-word tokenizers merge these greedily, so
    /// token boundaries shift when an overlap prefix is prepended — the case where a token-mode
    /// overlap cap can otherwise walk past the start of the chunk it is overlapping into.
    /// </summary>
    private static string RepeatedRuns(ref XorShift rng)
    {
        var sb = new StringBuilder();
        var runs = 1 + rng.Next(8);
        for (var r = 0; r < runs; r++)
        {
            var ch = (char)('a' + rng.Next(26));
            var length = 1 + rng.Next(40);
            sb.Append(ch, length);
            if (rng.Next(3) == 0) sb.Append(' ');
        }
        return sb.ToString();
    }

    private static string SeparatorsOnly(ref XorShift rng)
    {
        var sb = new StringBuilder();
        var count = 1 + rng.Next(30);
        for (var i = 0; i < count; i++)
            sb.Append(rng.Next(2) == 0 ? "\n\n" : " ");
        return sb.ToString();
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>
    /// Deterministic xorshift32. Hand-rolled rather than <see cref="Random"/> so the corpus is
    /// byte-identical on every target framework and runtime version.
    /// </summary>
    private struct XorShift
    {
        private uint _state;

        public XorShift(int seed) => _state = seed == 0 ? 0x9E3779B9u : (uint)seed;

        public uint Next()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        public int Next(int maxExclusive) => (int)(Next() % (uint)maxExclusive);
    }

    /// <summary>Counts Unicode scalar values in <c>text[start..end)</c>, the sizing unit of the character-mode chunkers.</summary>
    private static int ScalarCount(string text, int start, int end)
    {
        var count = 0;
        var i = start;
        while (i < end)
        {
            i += char.IsHighSurrogate(text[i]) && i + 1 < end && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
            count++;
        }
        return count;
    }

    private static async Task<List<TextChunk>> Collect(IAsyncEnumerable<TextChunk> source)
    {
        var results = new List<TextChunk>();
        await foreach (var chunk in source) results.Add(chunk);
        return results;
    }

    /// <summary>Renders the source text as an ASCII-safe C# literal so a failure can be pasted straight into a repro test.</summary>
    private static string Escape(string text)
    {
        var truncated = text.Length > MaxEscapedTextInFailureMessage;
        var slice = truncated ? text[..MaxEscapedTextInFailureMessage] : text;

        var sb = new StringBuilder(slice.Length + 16);
        foreach (var ch in slice)
        {
            if (ch is >= ' ' and <= '~' && ch != '\\' && ch != '"')
                sb.Append(ch);
            else
                sb.Append("\\u").Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
        }
        if (truncated) sb.Append("...<truncated>");
        return sb.ToString();
    }
}
