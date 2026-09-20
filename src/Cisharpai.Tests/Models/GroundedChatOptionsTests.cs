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
    public void DefaultCitationMode_IsFast()
    {
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Text: "Some text")]);

        Assert.That(options.CitationMode, Is.EqualTo(CitationMode.Fast));
    }

    [Test]
    public void CitationMode_CanBeSetToFast()
    {
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Text: "Some text")],
            CitationMode: CitationMode.Fast);

        Assert.That(options.CitationMode, Is.EqualTo(CitationMode.Fast));
    }

    [Test]
    public void Validate_InvalidChunk_BothDataAndText_ThrowsArgumentException()
    {
        var options = new GroundedChatOptions(
            Documents:
            [
                new DocumentChunk(
                    Data: new Dictionary<string, string> { ["snippet"] = "Some text" },
                    Text: "Also some text")
            ]);

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Test]
    public void Validate_InvalidChunk_NeitherDataNorText_ThrowsArgumentException()
    {
        var options = new GroundedChatOptions(
            Documents: [new DocumentChunk(Id: "doc-1")]);

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Test]
    public void Validate_MixOfValidAndInvalidChunks_ThrowsArgumentException()
    {
        var options = new GroundedChatOptions(
            Documents:
            [
                new DocumentChunk(Text: "Valid chunk"),
                new DocumentChunk(Id: "invalid-no-content")
            ]);

        Assert.Throws<ArgumentException>(() => options.Validate());
    }
}
