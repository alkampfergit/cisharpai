using Cisharpai.Rag.Evaluation;

namespace Cisharpai.Tests.Rag.Evaluation;

[TestFixture]
public class EvaluationScoreTests
{
    [Test]
    public void ValidScore_Succeeds()
    {
        var score = new EvaluationScore(0.5, "test");
        Assert.That(score.Score, Is.EqualTo(0.5));
        Assert.That(score.Rationale, Is.EqualTo("test"));
    }

    [Test]
    public void ZeroScore_Succeeds()
    {
        var score = new EvaluationScore(0.0, "none");
        Assert.That(score.Score, Is.EqualTo(0.0));
    }

    [Test]
    public void OneScore_Succeeds()
    {
        var score = new EvaluationScore(1.0, "perfect");
        Assert.That(score.Score, Is.EqualTo(1.0));
    }

    [Test]
    public void NegativeScore_Throws()
    {
        Assert.That(
            () => new EvaluationScore(-0.1, "bad"),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void OverOneScore_Throws()
    {
        Assert.That(
            () => new EvaluationScore(1.1, "too high"),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void NaN_Score_Throws()
    {
        Assert.That(
            () => new EvaluationScore(double.NaN, "nan"),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Infinity_Score_Throws()
    {
        Assert.That(
            () => new EvaluationScore(double.PositiveInfinity, "inf"),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void NullRationale_Throws()
    {
        Assert.That(
            () => new EvaluationScore(0.5, null!),
            Throws.ArgumentNullException);
    }

    [Test]
    public void RecordEquality_Works()
    {
        var a = new EvaluationScore(0.5, "test");
        var b = new EvaluationScore(0.5, "test");
        Assert.That(a, Is.EqualTo(b));
    }
}
