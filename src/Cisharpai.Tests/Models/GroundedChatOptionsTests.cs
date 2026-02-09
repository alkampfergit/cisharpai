using Cisharpai.Models;

namespace Cisharpai.Tests.Models;

public sealed class GroundedChatOptionsTests
{
    [Test]
    public void Validate_WithDocuments_DoesNotThrow()
    {
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Text: "Some text")]);

        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void Validate_EmptyDocuments_ThrowsArgumentException()
    {
        var options = new GroundedChatOptions(Documents: []);

        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("Documents"));
    }

    [Test]
    public void Validate_NullDocuments_ThrowsArgumentException()
    {
        var options = new GroundedChatOptions(Documents: null!);

        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("Documents"));
    }

    [Test]
    public void DefaultCitationMode_IsAccurate()
    {
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Text: "Some text")]);

        Assert.That(options.CitationMode, Is.EqualTo(CitationMode.Accurate));
    }

    [Test]
    public void CitationMode_CanBeSetToFast()
    {
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Text: "Some text")],
            CitationMode: CitationMode.Fast);

        Assert.That(options.CitationMode, Is.EqualTo(CitationMode.Fast));
    }
}
