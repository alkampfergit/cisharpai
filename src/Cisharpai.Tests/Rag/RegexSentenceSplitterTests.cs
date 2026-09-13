using Cisharpai.Rag.Chunking;

namespace Cisharpai.Tests.Rag;

[TestFixture]
public class RegexSentenceSplitterTests
{
    private readonly RegexSentenceSplitter _splitter = new();

    [Test]
    public void SplitOnPeriodFollowedByWhitespace()
    {
        var result = _splitter.Split("Hello world. Goodbye world.");
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Is.EqualTo("Hello world."));
        Assert.That(result[1], Is.EqualTo("Goodbye world."));
    }

    [Test]
    public void SplitOnExclamationAndQuestion()
    {
        var result = _splitter.Split("Wow! Really? Yes.");
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Is.EqualTo("Wow!"));
        Assert.That(result[1], Is.EqualTo("Really?"));
        Assert.That(result[2], Is.EqualTo("Yes."));
    }

    [Test]
    public void SingleSentence_ReturnsOne()
    {
        var result = _splitter.Split("Just one sentence.");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo("Just one sentence."));
    }

    [Test]
    public void EmptyString_ReturnsEmpty()
    {
        var result = _splitter.Split("");
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void WhitespaceOnly_ReturnsEmpty()
    {
        var result = _splitter.Split("   \n\t  ");
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _splitter.Split(null!));
    }

    [Test]
    public void NoTerminalPunctuation_ReturnsSingleEntry()
    {
        var result = _splitter.Split("No punctuation here");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo("No punctuation here"));
    }

    [Test]
    public void MultipleSpacesAfterPeriod_StillSplits()
    {
        var result = _splitter.Split("First.  Second.");
        Assert.That(result, Has.Count.EqualTo(2));
    }
}
