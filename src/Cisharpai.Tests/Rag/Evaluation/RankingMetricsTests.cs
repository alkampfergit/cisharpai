using Cisharpai.Rag.Evaluation;

namespace Cisharpai.Tests.Rag.Evaluation;

[TestFixture]
public class RankingMetricsTests
{
    // --- nDCG ---

    [Test]
    public void Ndcg_PerfectRanking_ReturnsOne()
    {
        var retrieved = new[] { "a", "b", "c" };
        var relevant = new HashSet<string> { "a", "b", "c" };

        Assert.That(RankingMetrics.Ndcg(retrieved, relevant), Is.EqualTo(1.0).Within(1e-10));
    }

    [Test]
    public void Ndcg_AllRelevantReversed_ReturnsOne()
    {
        var retrieved = new[] { "c", "b", "a" };
        var relevant = new HashSet<string> { "a", "b", "c" };

        var score = RankingMetrics.Ndcg(retrieved, relevant);
        Assert.That(score, Is.EqualTo(1.0).Within(1e-10),
            "All items relevant, order doesn't affect binary nDCG");
    }

    [Test]
    public void Ndcg_PartialRelevance_BetweenZeroAndOne()
    {
        var retrieved = new[] { "a", "x", "b", "y" };
        var relevant = new HashSet<string> { "a", "b" };

        var score = RankingMetrics.Ndcg(retrieved, relevant);
        Assert.That(score, Is.GreaterThan(0.0));
        Assert.That(score, Is.LessThan(1.0));
    }

    [Test]
    public void Ndcg_NoRelevantItems_ReturnsZero()
    {
        var retrieved = new[] { "a", "b" };
        var relevant = new HashSet<string>();

        Assert.That(RankingMetrics.Ndcg(retrieved, relevant), Is.EqualTo(0.0));
    }

    [Test]
    public void Ndcg_EmptyRetrieved_ReturnsZero()
    {
        var retrieved = Array.Empty<string>();
        var relevant = new HashSet<string> { "a" };

        Assert.That(RankingMetrics.Ndcg(retrieved, relevant), Is.EqualTo(0.0));
    }

    [Test]
    public void Ndcg_RelevantAtTop_ScoresHigherThanAtBottom()
    {
        var relevant = new HashSet<string> { "a" };
        var topRanked = new[] { "a", "x", "y" };
        var bottomRanked = new[] { "x", "y", "a" };

        Assert.That(
            RankingMetrics.Ndcg(topRanked, relevant),
            Is.GreaterThan(RankingMetrics.Ndcg(bottomRanked, relevant)));
    }

    [Test]
    public void Ndcg_DuplicateRetrievedIds_CountsEachRelevantOnce()
    {
        var retrieved = new[] { "a", "a" };
        var relevant = new HashSet<string> { "a" };

        var score = RankingMetrics.Ndcg(retrieved, relevant);
        Assert.That(score, Is.LessThanOrEqualTo(1.0),
            "Duplicate retrieved IDs must not inflate nDCG above 1.0");
    }

    [Test]
    public void Ndcg_CaseInsensitiveRelevantIds_UsesConsistentComparer()
    {
        var retrieved = new[] { "A", "a" };
        var relevant = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a" };

        var score = RankingMetrics.Ndcg(retrieved, relevant);
        Assert.That(score, Is.LessThanOrEqualTo(1.0),
            "Case-insensitive relevant set must not cause double-counting");
    }

    [Test]
    public void Ndcg_NullRetrieved_Throws()
    {
        Assert.That(
            () => RankingMetrics.Ndcg(null!, new HashSet<string> { "a" }),
            Throws.ArgumentNullException);
    }

    [Test]
    public void Ndcg_NullRelevant_Throws()
    {
        Assert.That(
            () => RankingMetrics.Ndcg(new[] { "a" }, (IReadOnlySet<string>)null!),
            Throws.ArgumentNullException);
    }

    [Test]
    public void Ndcg_ListOverload_Works()
    {
        var retrieved = new[] { "a", "b" };
        var relevant = new[] { "a", "b" };

        Assert.That(RankingMetrics.Ndcg(retrieved, relevant), Is.EqualTo(1.0).Within(1e-10));
    }

    // --- MRR ---

    [Test]
    public void Mrr_FirstItemRelevant_ReturnsOne()
    {
        var retrieved = new[] { "a", "b", "c" };
        var relevant = new HashSet<string> { "a" };

        Assert.That(RankingMetrics.Mrr(retrieved, relevant), Is.EqualTo(1.0));
    }

    [Test]
    public void Mrr_SecondItemRelevant_ReturnsHalf()
    {
        var retrieved = new[] { "x", "a", "y" };
        var relevant = new HashSet<string> { "a" };

        Assert.That(RankingMetrics.Mrr(retrieved, relevant), Is.EqualTo(0.5));
    }

    [Test]
    public void Mrr_ThirdItemRelevant_ReturnsThird()
    {
        var retrieved = new[] { "x", "y", "a" };
        var relevant = new HashSet<string> { "a" };

        Assert.That(RankingMetrics.Mrr(retrieved, relevant), Is.EqualTo(1.0 / 3.0).Within(1e-10));
    }

    [Test]
    public void Mrr_NoRelevantItems_ReturnsZero()
    {
        var retrieved = new[] { "x", "y" };
        var relevant = new HashSet<string> { "a" };

        Assert.That(RankingMetrics.Mrr(retrieved, relevant), Is.EqualTo(0.0));
    }

    [Test]
    public void Mrr_EmptyRetrieved_ReturnsZero()
    {
        var retrieved = Array.Empty<string>();
        var relevant = new HashSet<string> { "a" };

        Assert.That(RankingMetrics.Mrr(retrieved, relevant), Is.EqualTo(0.0));
    }

    [Test]
    public void Mrr_MultipleRelevant_ReturnsReciprocalOfFirst()
    {
        var retrieved = new[] { "x", "a", "b" };
        var relevant = new HashSet<string> { "a", "b" };

        Assert.That(RankingMetrics.Mrr(retrieved, relevant), Is.EqualTo(0.5));
    }

    [Test]
    public void Mrr_NullRetrieved_Throws()
    {
        Assert.That(
            () => RankingMetrics.Mrr(null!, new HashSet<string> { "a" }),
            Throws.ArgumentNullException);
    }

    [Test]
    public void Mrr_NullRelevant_Throws()
    {
        Assert.That(
            () => RankingMetrics.Mrr(new[] { "a" }, (IReadOnlySet<string>)null!),
            Throws.ArgumentNullException);
    }

    [Test]
    public void Mrr_ListOverload_Works()
    {
        var retrieved = new[] { "x", "a" };
        var relevant = new[] { "a" };

        Assert.That(RankingMetrics.Mrr(retrieved, relevant), Is.EqualTo(0.5));
    }

    // --- Recall@k ---

    [Test]
    public void RecallAtK_AllRelevantInTopK_ReturnsOne()
    {
        var retrieved = new[] { "a", "b", "c", "x" };
        var relevant = new HashSet<string> { "a", "b" };

        Assert.That(RankingMetrics.RecallAtK(retrieved, relevant, 4), Is.EqualTo(1.0));
    }

    [Test]
    public void RecallAtK_HalfRelevantInTopK()
    {
        var retrieved = new[] { "a", "x", "y", "b" };
        var relevant = new HashSet<string> { "a", "b" };

        Assert.That(RankingMetrics.RecallAtK(retrieved, relevant, 2), Is.EqualTo(0.5));
    }

    [Test]
    public void RecallAtK_NoneInTopK_ReturnsZero()
    {
        var retrieved = new[] { "x", "y", "a" };
        var relevant = new HashSet<string> { "a" };

        Assert.That(RankingMetrics.RecallAtK(retrieved, relevant, 2), Is.EqualTo(0.0));
    }

    [Test]
    public void RecallAtK_KLargerThanRetrieved_StopsAtEnd()
    {
        var retrieved = new[] { "a" };
        var relevant = new HashSet<string> { "a", "b" };

        Assert.That(RankingMetrics.RecallAtK(retrieved, relevant, 100), Is.EqualTo(0.5));
    }

    [Test]
    public void RecallAtK_NoRelevantItems_ReturnsZero()
    {
        var retrieved = new[] { "a", "b" };
        var relevant = new HashSet<string>();

        Assert.That(RankingMetrics.RecallAtK(retrieved, relevant, 2), Is.EqualTo(0.0));
    }

    [Test]
    public void RecallAtK_ZeroK_Throws()
    {
        Assert.That(
            () => RankingMetrics.RecallAtK(new[] { "a" }, new HashSet<string> { "a" }, 0),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void RecallAtK_NegativeK_Throws()
    {
        Assert.That(
            () => RankingMetrics.RecallAtK(new[] { "a" }, new HashSet<string> { "a" }, -1),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void RecallAtK_NullRetrieved_Throws()
    {
        Assert.That(
            () => RankingMetrics.RecallAtK(null!, new HashSet<string> { "a" }, 1),
            Throws.ArgumentNullException);
    }

    [Test]
    public void RecallAtK_NullRelevant_Throws()
    {
        Assert.That(
            () => RankingMetrics.RecallAtK(new[] { "a" }, (IReadOnlySet<string>)null!, 1),
            Throws.ArgumentNullException);
    }

    [Test]
    public void RecallAtK_ListOverload_Works()
    {
        var retrieved = new[] { "a", "b" };
        var relevant = new[] { "a", "b" };

        Assert.That(RankingMetrics.RecallAtK(retrieved, relevant, 2), Is.EqualTo(1.0));
    }

    [Test]
    public void RecallAtK_K1_OnlyFirstItem()
    {
        var retrieved = new[] { "a", "b" };
        var relevant = new HashSet<string> { "b" };

        Assert.That(RankingMetrics.RecallAtK(retrieved, relevant, 1), Is.EqualTo(0.0));
    }

    [Test]
    public void RecallAtK_DuplicateRetrievedIds_CountsEachRelevantOnce()
    {
        var retrieved = new[] { "a", "a" };
        var relevant = new HashSet<string> { "a" };

        Assert.That(RankingMetrics.RecallAtK(retrieved, relevant, 2), Is.LessThanOrEqualTo(1.0));
        Assert.That(RankingMetrics.RecallAtK(retrieved, relevant, 2), Is.EqualTo(1.0));
    }

    [Test]
    public void RecallAtK_CaseInsensitiveRelevantIds_UsesConsistentComparer()
    {
        var retrieved = new[] { "A", "a" };
        var relevant = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a" };

        Assert.That(RankingMetrics.RecallAtK(retrieved, relevant, 2), Is.LessThanOrEqualTo(1.0),
            "Case-insensitive relevant set must not cause double-counting");
    }
}
