using Cisharpai.Rag;
using Cisharpai.Rag.Models;
using Cisharpai.Rag.Packing;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class RankFusionTests
{
    private static TextChunk MakeChunk(string docId, int index) =>
        new(docId, index, 0, 1, "x");

    private static ScoredChunk Scored(string docId, int index, double score) =>
        new(MakeChunk(docId, index), score);

    [Test]
    public void ReciprocalRank_ItemInBothLists_ScoresHigherThanSingleList()
    {
        var list1 = new[] { Scored("doc", 0, 0.9), Scored("doc", 1, 0.8) };
        var list2 = new[] { Scored("doc", 0, 0.95), Scored("doc", 2, 0.7) };

        var fused = RankFusion.ReciprocalRank(new[] { list1, list2 });

        Assert.That(fused[0].Chunk.DocumentId, Is.EqualTo("doc"));
        Assert.That(fused[0].Chunk.Index, Is.EqualTo(0));

        var scoreDoc0 = fused.First(c => c.Chunk.Index == 0).Score;
        var scoreDoc1 = fused.First(c => c.Chunk.Index == 1).Score;
        var scoreDoc2 = fused.First(c => c.Chunk.Index == 2).Score;

        Assert.That(scoreDoc0, Is.GreaterThan(scoreDoc1));
        Assert.That(scoreDoc0, Is.GreaterThan(scoreDoc2));
    }

    [Test]
    public void ReciprocalRank_ItemHighInOneList_AbsentFromAnother()
    {
        var list1 = new[] { Scored("doc", 0, 0.9), Scored("doc", 1, 0.8) };
        var list2 = new[] { Scored("doc", 2, 0.95), Scored("doc", 3, 0.7) };

        var fused = RankFusion.ReciprocalRank(new[] { list1, list2 });

        Assert.That(fused, Has.Count.EqualTo(4));
        var doc0Score = fused.First(c => c.Chunk.Index == 0).Score;
        var doc2Score = fused.First(c => c.Chunk.Index == 2).Score;
        Assert.That(doc0Score, Is.EqualTo(doc2Score).Within(1e-10),
            "Items ranked first in their respective lists should have equal RRF scores");
    }

    [Test]
    public void ReciprocalRank_IdenticalLists_PreservesOrder()
    {
        var items = new[]
        {
            Scored("doc", 0, 0.9),
            Scored("doc", 1, 0.8),
            Scored("doc", 2, 0.7)
        };
        var list1 = items.ToArray();
        var list2 = items.ToArray();

        var fused = RankFusion.ReciprocalRank(new[] { list1, list2 });

        Assert.That(fused, Has.Count.EqualTo(3));
        Assert.That(fused[0].Chunk.Index, Is.EqualTo(0));
        Assert.That(fused[1].Chunk.Index, Is.EqualTo(1));
        Assert.That(fused[2].Chunk.Index, Is.EqualTo(2));
    }

    [Test]
    public void ReciprocalRank_SingleList_ReturnsRRFScores()
    {
        var list = new[]
        {
            Scored("doc", 0, 0.9),
            Scored("doc", 1, 0.5)
        };

        var fused = RankFusion.ReciprocalRank(new[] { list });

        Assert.That(fused, Has.Count.EqualTo(2));
        Assert.That(fused[0].Score, Is.GreaterThan(fused[1].Score));
        Assert.That(fused[0].Score, Is.EqualTo(1.0 / 61.0).Within(1e-10));
        Assert.That(fused[1].Score, Is.EqualTo(1.0 / 62.0).Within(1e-10));
    }

    [Test]
    public void ReciprocalRank_EmptyLists_ReturnsEmpty()
    {
        var fused = RankFusion.ReciprocalRank(new IReadOnlyList<ScoredChunk>[]
        {
            Array.Empty<ScoredChunk>(),
            Array.Empty<ScoredChunk>()
        });

        Assert.That(fused, Is.Empty);
    }

    [Test]
    public void ReciprocalRank_NullInput_Throws()
    {
        Assert.That(
            () => RankFusion.ReciprocalRank(null!),
            Throws.ArgumentNullException);
    }

    [Test]
    public void ReciprocalRank_InvalidK_Throws()
    {
        var list = new[] { Scored("doc", 0, 0.9) };

        Assert.That(
            () => RankFusion.ReciprocalRank(new[] { list }, k: 0),
            Throws.TypeOf<ArgumentOutOfRangeException>());

        Assert.That(
            () => RankFusion.ReciprocalRank(new[] { list }, k: -1),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ReciprocalRank_NullListSkipped()
    {
        var list1 = new[] { Scored("doc", 0, 0.9) };

        var fused = RankFusion.ReciprocalRank(new IReadOnlyList<ScoredChunk>[] { list1, null! });

        Assert.That(fused, Has.Count.EqualTo(1));
    }

    [Test]
    public void ReciprocalRank_CustomK_ChangesScores()
    {
        var list = new[] { Scored("doc", 0, 0.9) };

        var defaultK = RankFusion.ReciprocalRank(new[] { list });
        var smallK = RankFusion.ReciprocalRank(new[] { list }, k: 1);

        Assert.That(smallK[0].Score, Is.GreaterThan(defaultK[0].Score));
        Assert.That(smallK[0].Score, Is.EqualTo(1.0 / 2.0).Within(1e-10));
    }

    [Test]
    public void ReciprocalRank_ReversedLists_ExtremeItemsTieAndBeatMiddle()
    {
        var list1 = new[]
        {
            Scored("doc", 0, 0.9),
            Scored("doc", 1, 0.5),
            Scored("doc", 2, 0.1)
        };
        var list2 = new[]
        {
            Scored("doc", 2, 0.9),
            Scored("doc", 1, 0.5),
            Scored("doc", 0, 0.1)
        };

        var fused = RankFusion.ReciprocalRank(new[] { list1, list2 });

        var scoreDoc0 = fused.First(c => c.Chunk.Index == 0).Score;
        var scoreDoc1 = fused.First(c => c.Chunk.Index == 1).Score;
        var scoreDoc2 = fused.First(c => c.Chunk.Index == 2).Score;

        Assert.That(scoreDoc0, Is.EqualTo(scoreDoc2).Within(1e-10),
            "Items ranked 1st and 3rd in opposite lists should have equal total RRF scores");
        Assert.That(scoreDoc0, Is.GreaterThan(scoreDoc1),
            "1/(k+1)+1/(k+3) > 2/(k+2) by the convexity of 1/x");
    }

    [Test]
    public void ReciprocalRank_ItemInMoreLists_Wins()
    {
        var list1 = new[] { Scored("doc", 0, 1.0), Scored("doc", 1, 0.5) };
        var list2 = new[] { Scored("doc", 0, 1.0), Scored("doc", 2, 0.5) };
        var list3 = new[] { Scored("doc", 0, 1.0), Scored("doc", 3, 0.5) };

        var fused = RankFusion.ReciprocalRank(new[] { list1, list2, list3 });

        Assert.That(fused[0].Chunk.Index, Is.EqualTo(0),
            "Item appearing at rank 1 in all 3 lists should clearly beat any item in only 1 list");
    }

    [Test]
    public void ReciprocalRank_ThreeLists_Fuses()
    {
        var list1 = new[] { Scored("a", 0, 1.0) };
        var list2 = new[] { Scored("b", 0, 1.0) };
        var list3 = new[] { Scored("a", 0, 1.0), Scored("b", 0, 0.5) };

        var fused = RankFusion.ReciprocalRank(new[] { list1, list2, list3 });

        Assert.That(fused[0].Chunk.DocumentId, Is.EqualTo("a"),
            "Item appearing in 2 lists at rank 1 should beat item appearing in 2 lists at ranks 1 and 2");
    }

    [Test]
    public void ReciprocalRank_ScoreIsSumOfRRF_NotOriginalScore()
    {
        var list1 = new[] { Scored("doc", 0, 999.0) };

        var fused = RankFusion.ReciprocalRank(new[] { list1 });

        Assert.That(fused[0].Score, Is.EqualTo(1.0 / 61.0).Within(1e-10),
            "RRF score should be 1/(k+rank), not the original score");
    }

    [Test]
    public void ReciprocalRank_NaN_K_Throws()
    {
        var list = new[] { Scored("doc", 0, 0.9) };

        Assert.That(
            () => RankFusion.ReciprocalRank(new[] { list }, k: double.NaN),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ReciprocalRank_PositiveInfinity_K_Throws()
    {
        var list = new[] { Scored("doc", 0, 0.9) };

        Assert.That(
            () => RankFusion.ReciprocalRank(new[] { list }, k: double.PositiveInfinity),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ReciprocalRank_NegativeInfinity_K_Throws()
    {
        var list = new[] { Scored("doc", 0, 0.9) };

        Assert.That(
            () => RankFusion.ReciprocalRank(new[] { list }, k: double.NegativeInfinity),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ReciprocalRank_DifferentDocuments_IdentifiedCorrectly()
    {
        var list1 = new[] { Scored("docA", 0, 0.9), Scored("docB", 0, 0.5) };
        var list2 = new[] { Scored("docB", 0, 0.9), Scored("docA", 0, 0.5) };

        var fused = RankFusion.ReciprocalRank(new[] { list1, list2 });

        Assert.That(fused, Has.Count.EqualTo(2));
        var scoreA = fused.First(c => c.Chunk.DocumentId == "docA").Score;
        var scoreB = fused.First(c => c.Chunk.DocumentId == "docB").Score;
        Assert.That(scoreA, Is.EqualTo(scoreB).Within(1e-10));
    }
}
