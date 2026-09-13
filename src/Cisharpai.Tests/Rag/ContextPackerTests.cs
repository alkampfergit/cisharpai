using Cisharpai.Rag.Models;
using Cisharpai.Rag.Packing;
using Cisharpai.Testing;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class ContextPackerTests
{
    private static TextChunk MakeChunk(string text, int index = 0) =>
        new("doc", index, 0, text.Length, text);

    private static ScoredChunk Scored(string text, double score, int index = 0) =>
        new(MakeChunk(text, index), score);

    [Test]
    public async Task PackAsync_AllChunksFit_ReturnsAllSelected()
    {
        var counter = new FakeTokenCounter { DefaultCount = 10 };
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("chunk-a", 0.9, 0),
            Scored("chunk-b", 0.8, 1),
            Scored("chunk-c", 0.7, 2)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 100,
            ReservedTokens = 0,
            Separator = "\n\n",
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Selected, Has.Count.EqualTo(3));
            Assert.That(result.Dropped, Is.Empty);
            // 3 chunks × 10 tokens + 2 separators × 10 tokens = 50
            Assert.That(result.TotalTokensUsed, Is.EqualTo(50));
            Assert.That(result.BudgetRemaining, Is.EqualTo(50));
        });
    }

    [Test]
    public async Task PackAsync_RankOrderPreservedWhenLostInMiddleDisabled()
    {
        var counter = new FakeTokenCounter { DefaultCount = 5 };
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("a", 0.9, 0),
            Scored("b", 0.8, 1),
            Scored("c", 0.7, 2),
            Scored("d", 0.6, 3)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 200,
            ReservedTokens = 0,
            Separator = "\n\n",
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        var selectedTexts = result.Selected.Select(s => s.Chunk.Text).ToList();
        Assert.That(selectedTexts, Is.EqualTo(new[] { "a", "b", "c", "d" }));
    }

    [Test]
    public async Task PackAsync_LostInMiddleOrdering_HighestAtEdgesWeakestInMiddle()
    {
        var counter = new FakeTokenCounter { DefaultCount = 5 };
        var packer = new ContextPacker(counter);
        // Rank order: A (best) > B > C > D > E (worst)
        var chunks = new[]
        {
            Scored("A", 0.9, 0),
            Scored("B", 0.8, 1),
            Scored("C", 0.7, 2),
            Scored("D", 0.6, 3),
            Scored("E", 0.5, 4)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 500,
            ReservedTokens = 0,
            Separator = "",
            UseLostInMiddleOrdering = true
        };

        var result = await packer.PackAsync(chunks, options);

        // Expected lost-in-middle: A(0), C(2), E(4), D(3), B(1)
        // Even indices fill from left: A, C, E
        // Odd indices fill from right: B, D
        var selectedTexts = result.Selected.Select(s => s.Chunk.Text).ToList();
        Assert.That(selectedTexts, Is.EqualTo(new[] { "A", "C", "E", "D", "B" }));
    }

    [Test]
    public async Task PackAsync_LostInMiddleOrdering_TwoChunks_PreservesOrder()
    {
        var counter = new FakeTokenCounter { DefaultCount = 5 };
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("first", 0.9, 0),
            Scored("second", 0.8, 1)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 500,
            ReservedTokens = 0,
            Separator = "",
            UseLostInMiddleOrdering = true
        };

        var result = await packer.PackAsync(chunks, options);

        var selectedTexts = result.Selected.Select(s => s.Chunk.Text).ToList();
        Assert.That(selectedTexts, Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public async Task PackAsync_BudgetExhausted_DropsLowerRankedChunks()
    {
        var counter = new FakeTokenCounter();
        counter.EnqueueCount(2); // separator
        counter.EnqueueCount(20); // chunk A
        counter.EnqueueCount(20); // chunk B
        counter.EnqueueCount(20); // chunk C
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("A", 0.9, 0),
            Scored("B", 0.8, 1),
            Scored("C", 0.7, 2)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 50,
            ReservedTokens = 0,
            Separator = "\n\n",
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.Multiple(() =>
        {
            // A (20) + separator (2) + B (20) = 42, C (20) + sep (2) = 22 > 8 remaining
            Assert.That(result.Selected, Has.Count.EqualTo(2));
            Assert.That(result.Dropped, Has.Count.EqualTo(1));
            Assert.That(result.Dropped[0].Chunk.Chunk.Text, Is.EqualTo("C"));
            Assert.That(result.Dropped[0].Reason, Is.EqualTo(DropReason.BudgetExhausted));
            Assert.That(result.Dropped[0].TokenCount, Is.EqualTo(20));
            Assert.That(result.TotalTokensUsed, Is.EqualTo(42));
            Assert.That(result.BudgetRemaining, Is.EqualTo(8));
        });
    }

    [Test]
    public async Task PackAsync_IndividuallyOversizedChunk_SkippedAndReported()
    {
        var counter = new FakeTokenCounter();
        counter.EnqueueCount(1); // separator
        counter.EnqueueCount(10); // chunk A
        counter.EnqueueCount(500); // chunk B — oversized
        counter.EnqueueCount(10); // chunk C
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("A", 0.9, 0),
            Scored("B-huge", 0.8, 1),
            Scored("C", 0.7, 2)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 50,
            ReservedTokens = 0,
            Separator = "|",
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Selected, Has.Count.EqualTo(2));
            Assert.That(result.Selected[0].Chunk.Text, Is.EqualTo("A"));
            Assert.That(result.Selected[1].Chunk.Text, Is.EqualTo("C"));
            Assert.That(result.Dropped, Has.Count.EqualTo(1));
            Assert.That(result.Dropped[0].Chunk.Chunk.Text, Is.EqualTo("B-huge"));
            Assert.That(result.Dropped[0].Reason, Is.EqualTo(DropReason.IndividuallyOversized));
            Assert.That(result.Dropped[0].TokenCount, Is.EqualTo(500));
        });
    }

    [Test]
    public async Task PackAsync_StopAtFirstMisfit_StopsPacking()
    {
        var counter = new FakeTokenCounter();
        counter.EnqueueCount(1); // separator
        counter.EnqueueCount(10); // chunk A
        counter.EnqueueCount(15); // chunk B — fits budget individually but not remaining space
        counter.EnqueueCount(5);  // chunk C — would fit but stop mode halts
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("A", 0.9, 0),
            Scored("B", 0.8, 1),
            Scored("C", 0.7, 2)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 20,
            ReservedTokens = 0,
            Separator = "|",
            OverflowStrategy = OverflowStrategy.StopAtFirstMisfit,
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.Multiple(() =>
        {
            // A=10, B=15+1=16 > 10 remaining => stop
            Assert.That(result.Selected, Has.Count.EqualTo(1));
            Assert.That(result.Selected[0].Chunk.Text, Is.EqualTo("A"));
            Assert.That(result.Dropped, Has.Count.EqualTo(2));
            Assert.That(result.Dropped[0].Chunk.Chunk.Text, Is.EqualTo("B"));
            Assert.That(result.Dropped[0].Reason, Is.EqualTo(DropReason.BudgetExhausted));
            Assert.That(result.Dropped[1].Chunk.Chunk.Text, Is.EqualTo("C"));
            Assert.That(result.Dropped[1].Reason, Is.EqualTo(DropReason.BudgetExhausted));
        });
    }

    [Test]
    public async Task PackAsync_StopAtFirstMisfit_OversizedDoesNotStop()
    {
        var counter = new FakeTokenCounter();
        counter.EnqueueCount(1); // separator
        counter.EnqueueCount(10); // chunk A
        counter.EnqueueCount(500); // chunk B — individually oversized (skipped even in StopAtFirstMisfit)
        counter.EnqueueCount(5); // chunk C — fits
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("A", 0.9, 0),
            Scored("B-huge", 0.8, 1),
            Scored("C", 0.7, 2)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 20,
            ReservedTokens = 0,
            Separator = "|",
            OverflowStrategy = OverflowStrategy.StopAtFirstMisfit,
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Selected, Has.Count.EqualTo(2));
            Assert.That(result.Selected[0].Chunk.Text, Is.EqualTo("A"));
            Assert.That(result.Selected[1].Chunk.Text, Is.EqualTo("C"));
            Assert.That(result.Dropped, Has.Count.EqualTo(1));
            Assert.That(result.Dropped[0].Reason, Is.EqualTo(DropReason.IndividuallyOversized));
        });
    }

    [Test]
    public async Task PackAsync_ReservedTokensReduceEffectiveBudget()
    {
        var counter = new FakeTokenCounter { DefaultCount = 10 };
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("A", 0.9, 0),
            Scored("B", 0.8, 1)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 100,
            ReservedTokens = 80,
            Separator = "\n\n",
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.Multiple(() =>
        {
            // Effective budget is 20. A=10 fits; B=10+sep=10 => 20 > 10 remaining, doesn't fit.
            Assert.That(result.Selected, Has.Count.EqualTo(1));
            Assert.That(result.Dropped, Has.Count.EqualTo(1));
            Assert.That(result.TotalTokensUsed, Is.EqualTo(10));
            Assert.That(result.BudgetRemaining, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task PackAsync_SeparatorAccountedNMinus1Times()
    {
        var counter = new FakeTokenCounter();
        counter.EnqueueCount(5); // separator
        counter.EnqueueCount(10); // chunk A
        counter.EnqueueCount(10); // chunk B
        counter.EnqueueCount(10); // chunk C
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("A", 0.9, 0),
            Scored("B", 0.8, 1),
            Scored("C", 0.7, 2)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 40,
            ReservedTokens = 0,
            Separator = "---",
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.Multiple(() =>
        {
            // A=10, sep+B=15, sep+C=15 => total=40
            Assert.That(result.Selected, Has.Count.EqualTo(3));
            Assert.That(result.TotalTokensUsed, Is.EqualTo(40));
            Assert.That(result.BudgetRemaining, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task PackAsync_EmptyInput_ReturnsEmptyResult()
    {
        var counter = new FakeTokenCounter { DefaultCount = 10 };
        var packer = new ContextPacker(counter);
        var options = new ContextPackingOptions
        {
            TokenBudget = 100,
            ReservedTokens = 0,
            Separator = "\n\n"
        };

        var result = await packer.PackAsync([], options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Selected, Is.Empty);
            Assert.That(result.Dropped, Is.Empty);
            Assert.That(result.TotalTokensUsed, Is.EqualTo(0));
            Assert.That(result.BudgetRemaining, Is.EqualTo(100));
        });
    }

    [Test]
    public async Task PackAsync_EmptySeparator_NoSeparatorTokensCounted()
    {
        var counter = new FakeTokenCounter { DefaultCount = 10 };
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("A", 0.9, 0),
            Scored("B", 0.8, 1),
            Scored("C", 0.7, 2)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 100,
            ReservedTokens = 0,
            Separator = "",
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Selected, Has.Count.EqualTo(3));
            // 3 chunks × 10 tokens, no separator cost
            Assert.That(result.TotalTokensUsed, Is.EqualTo(30));
            Assert.That(result.BudgetRemaining, Is.EqualTo(70));
            // Counter was called 3 times (chunks only, no separator call)
            Assert.That(counter.CallCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task PackAsync_CountsEachChunkExactlyOnce()
    {
        var counter = new FakeTokenCounter { DefaultCount = 10 };
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("chunk-a", 0.9, 0),
            Scored("chunk-b", 0.8, 1),
            Scored("chunk-c", 0.7, 2)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 500,
            ReservedTokens = 0,
            Separator = "\n\n"
        };

        await packer.PackAsync(chunks, options);

        // 1 separator + 3 chunks = 4 counter calls
        Assert.That(counter.CallCount, Is.EqualTo(4));
        Assert.That(counter.ReceivedTexts, Does.Contain("\n\n"));
        Assert.That(counter.ReceivedTexts, Does.Contain("chunk-a"));
        Assert.That(counter.ReceivedTexts, Does.Contain("chunk-b"));
        Assert.That(counter.ReceivedTexts, Does.Contain("chunk-c"));
    }

    [Test]
    public async Task PackAsync_SkipAndContinue_SkipsBudgetExhaustedButKeepsSmaller()
    {
        var counter = new FakeTokenCounter();
        counter.EnqueueCount(1); // separator
        counter.EnqueueCount(10); // rank 1
        counter.EnqueueCount(10); // rank 2
        counter.EnqueueCount(25); // rank 3 — too big for remaining budget
        counter.EnqueueCount(5);  // rank 4 — still fits
        counter.EnqueueCount(5);  // rank 5 — still fits
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("r1", 0.9, 0),
            Scored("r2", 0.8, 1),
            Scored("r3-big", 0.7, 2),
            Scored("r4", 0.6, 3),
            Scored("r5", 0.5, 4)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 40,
            ReservedTokens = 0,
            Separator = "|",
            OverflowStrategy = OverflowStrategy.SkipAndContinue,
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.Multiple(() =>
        {
            // r1=10, r2=10+1=11, r3=25+1=26>19 skip, r4=5+1=6, r5=5+1=6 => total=10+11+6+6=33
            Assert.That(result.Selected.Select(s => s.Chunk.Text).ToList(),
                Is.EqualTo(new[] { "r1", "r2", "r4", "r5" }));
            Assert.That(result.Dropped, Has.Count.EqualTo(1));
            Assert.That(result.Dropped[0].Chunk.Chunk.Text, Is.EqualTo("r3-big"));
            Assert.That(result.Dropped[0].Reason, Is.EqualTo(DropReason.BudgetExhausted));
            Assert.That(result.TotalTokensUsed, Is.EqualTo(33));
        });
    }

    [Test]
    public async Task PackAsync_CancellationRespected()
    {
        var counter = new FakeTokenCounter { DefaultCount = 10 };
        var packer = new ContextPacker(counter);
        var chunks = new[] { Scored("A", 0.9, 0) };
        var options = new ContextPackingOptions
        {
            TokenBudget = 100,
            ReservedTokens = 0,
            Separator = "\n\n"
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(
            () => packer.PackAsync(chunks, options, cts.Token));
    }

    // --- Options validation tests ---

    [Test]
    public void Options_ZeroTokenBudget_Throws()
    {
        var counter = new FakeTokenCounter { DefaultCount = 1 };
        var packer = new ContextPacker(counter);
        var options = new ContextPackingOptions { TokenBudget = 0 };

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => packer.PackAsync([], options));
    }

    [Test]
    public void Options_NegativeTokenBudget_Throws()
    {
        var counter = new FakeTokenCounter { DefaultCount = 1 };
        var packer = new ContextPacker(counter);
        var options = new ContextPackingOptions { TokenBudget = -1 };

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => packer.PackAsync([], options));
    }

    [Test]
    public void Options_NegativeReservedTokens_Throws()
    {
        var counter = new FakeTokenCounter { DefaultCount = 1 };
        var packer = new ContextPacker(counter);
        var options = new ContextPackingOptions { TokenBudget = 100, ReservedTokens = -1 };

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => packer.PackAsync([], options));
    }

    [Test]
    public void Options_ReservedTokensEqualsBudget_Throws()
    {
        var counter = new FakeTokenCounter { DefaultCount = 1 };
        var packer = new ContextPacker(counter);
        var options = new ContextPackingOptions { TokenBudget = 100, ReservedTokens = 100 };

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => packer.PackAsync([], options));
    }

    [Test]
    public void Options_ReservedTokensExceedsBudget_Throws()
    {
        var counter = new FakeTokenCounter { DefaultCount = 1 };
        var packer = new ContextPacker(counter);
        var options = new ContextPackingOptions { TokenBudget = 100, ReservedTokens = 150 };

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => packer.PackAsync([], options));
    }

    [Test]
    public void Options_NullSeparator_Throws()
    {
        var counter = new FakeTokenCounter { DefaultCount = 1 };
        var packer = new ContextPacker(counter);
        var options = new ContextPackingOptions { TokenBudget = 100, Separator = null! };

        Assert.ThrowsAsync<ArgumentNullException>(
            () => packer.PackAsync([], options));
    }

    [Test]
    public void Options_InvalidOverflowStrategy_Throws()
    {
        var counter = new FakeTokenCounter { DefaultCount = 1 };
        var packer = new ContextPacker(counter);
        var options = new ContextPackingOptions
        {
            TokenBudget = 100,
            OverflowStrategy = (OverflowStrategy)999
        };

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => packer.PackAsync([], options));
    }

    [Test]
    public void Constructor_NullCounter_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ContextPacker(null!));
    }

    [Test]
    public void PackAsync_NullChunks_Throws()
    {
        var counter = new FakeTokenCounter { DefaultCount = 1 };
        var packer = new ContextPacker(counter);
        var options = new ContextPackingOptions { TokenBudget = 100 };

        Assert.ThrowsAsync<ArgumentNullException>(
            () => packer.PackAsync(null!, options));
    }

    [Test]
    public void PackAsync_NullOptions_Throws()
    {
        var counter = new FakeTokenCounter { DefaultCount = 1 };
        var packer = new ContextPacker(counter);

        Assert.ThrowsAsync<ArgumentNullException>(
            () => packer.PackAsync([], null!));
    }

    [Test]
    public async Task PackAsync_StopAtFirstMisfit_LabelsOversizedChunksCorrectly()
    {
        var counter = new FakeTokenCounter();
        counter.EnqueueCount(1);   // separator
        counter.EnqueueCount(10);  // chunk A
        counter.EnqueueCount(15);  // chunk B — fits budget individually but not remaining space
        counter.EnqueueCount(500); // chunk C — individually oversized (exceeds effective budget of 20)
        counter.EnqueueCount(5);   // chunk D — fits budget individually
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("A", 0.9, 0),
            Scored("B", 0.8, 1),
            Scored("C-huge", 0.7, 2),
            Scored("D", 0.6, 3)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 20,
            ReservedTokens = 0,
            Separator = "|",
            OverflowStrategy = OverflowStrategy.StopAtFirstMisfit,
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Selected, Has.Count.EqualTo(1));
            Assert.That(result.Dropped, Has.Count.EqualTo(3));
            Assert.That(result.Dropped[0].Chunk.Chunk.Text, Is.EqualTo("B"));
            Assert.That(result.Dropped[0].Reason, Is.EqualTo(DropReason.BudgetExhausted));
            Assert.That(result.Dropped[1].Chunk.Chunk.Text, Is.EqualTo("C-huge"));
            Assert.That(result.Dropped[1].Reason, Is.EqualTo(DropReason.IndividuallyOversized));
            Assert.That(result.Dropped[2].Chunk.Chunk.Text, Is.EqualTo("D"));
            Assert.That(result.Dropped[2].Reason, Is.EqualTo(DropReason.BudgetExhausted));
        });
    }

    [Test]
    public void PackAsync_CancellationRespected_EmptyInput()
    {
        var counter = new FakeTokenCounter { DefaultCount = 10 };
        var packer = new ContextPacker(counter);
        var options = new ContextPackingOptions
        {
            TokenBudget = 100,
            ReservedTokens = 0,
            Separator = ""
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(
            () => packer.PackAsync([], options, cts.Token));
    }

    [Test]
    public async Task PackAsync_SingleChunk_DoesNotCountSeparator()
    {
        var counter = new FakeTokenCounter { DefaultCount = 10 };
        var packer = new ContextPacker(counter);
        var chunks = new[] { Scored("only", 1.0) };
        var options = new ContextPackingOptions
        {
            TokenBudget = 100,
            ReservedTokens = 0,
            Separator = "\n\n",
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Selected, Has.Count.EqualTo(1));
            Assert.That(counter.CallCount, Is.EqualTo(1));
            Assert.That(counter.ReceivedTexts, Does.Not.Contain("\n\n"));
        });
    }

    [Test]
    public async Task PackAsync_ResultCollectionsAreGenuinelyImmutable()
    {
        var counter = new FakeTokenCounter();
        counter.EnqueueCount(1);  // separator
        counter.EnqueueCount(10); // chunk A
        counter.EnqueueCount(10); // chunk B
        counter.EnqueueCount(10); // chunk C — won't fit
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("A", 0.9, 0),
            Scored("B", 0.8, 1),
            Scored("C", 0.7, 2)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 25,
            ReservedTokens = 0,
            Separator = "|",
            UseLostInMiddleOrdering = false
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Selected, Is.Not.InstanceOf<List<ScoredChunk>>());
            Assert.That(result.Selected, Is.Not.InstanceOf<ScoredChunk[]>());
            Assert.That(result.Dropped, Is.Not.InstanceOf<List<DroppedChunk>>());
        });
    }

    [Test]
    public async Task PackAsync_LostInMiddle_SingleChunk_ReturnsAsIs()
    {
        var counter = new FakeTokenCounter { DefaultCount = 5 };
        var packer = new ContextPacker(counter);
        var chunks = new[] { Scored("only", 1.0) };
        var options = new ContextPackingOptions
        {
            TokenBudget = 500,
            ReservedTokens = 0,
            Separator = "",
            UseLostInMiddleOrdering = true
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.That(result.Selected.Select(c => c.Chunk.Text), Is.EqualTo(new[] { "only" }));
    }

    [Test]
    public async Task PackAsync_LostInMiddle_ThreeChunks_CorrectOrder()
    {
        var counter = new FakeTokenCounter { DefaultCount = 5 };
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("A", 0.9, 0),
            Scored("B", 0.8, 1),
            Scored("C", 0.7, 2)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 500,
            ReservedTokens = 0,
            Separator = "",
            UseLostInMiddleOrdering = true
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.That(result.Selected.Select(c => c.Chunk.Text),
            Is.EqualTo(new[] { "A", "C", "B" }));
    }

    [Test]
    public async Task PackAsync_LostInMiddle_FourChunks_CorrectOrder()
    {
        var counter = new FakeTokenCounter { DefaultCount = 5 };
        var packer = new ContextPacker(counter);
        var chunks = new[]
        {
            Scored("1st", 0.9, 0),
            Scored("2nd", 0.8, 1),
            Scored("3rd", 0.7, 2),
            Scored("4th", 0.6, 3)
        };
        var options = new ContextPackingOptions
        {
            TokenBudget = 500,
            ReservedTokens = 0,
            Separator = "",
            UseLostInMiddleOrdering = true
        };

        var result = await packer.PackAsync(chunks, options);

        Assert.That(result.Selected.Select(c => c.Chunk.Text),
            Is.EqualTo(new[] { "1st", "3rd", "4th", "2nd" }));
    }
}
