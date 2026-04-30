using Cisharpai.Models;

namespace Cisharpai.Tests.Models;

public sealed class DocumentChunkTests
{
    [Test]
    public void Validate_WithData_DoesNotThrow()
    {
        var chunk = new DocumentChunk(
            Data: new Dictionary<string, string> { ["title"] = "Test", ["snippet"] = "Some text" });

        Assert.DoesNotThrow(() => chunk.Validate());
    }

    [Test]
    public void Validate_WithText_DoesNotThrow()
    {
        var chunk = new DocumentChunk(Text: "Some plain text content");

        Assert.DoesNotThrow(() => chunk.Validate());
    }

    [Test]
    public void Validate_WithDataAndId_DoesNotThrow()
    {
        var chunk = new DocumentChunk(
            Id: "doc-1",
            Data: new Dictionary<string, string> { ["snippet"] = "Some text" });

        Assert.DoesNotThrow(() => chunk.Validate());
    }

    [Test]
    public void Validate_BothDataAndText_ThrowsArgumentException()
    {
        var chunk = new DocumentChunk(
            Data: new Dictionary<string, string> { ["snippet"] = "Some text" },
            Text: "Also some text");

        Assert.Throws<ArgumentException>(() => chunk.Validate());
    }

    [Test]
    public void Validate_NeitherDataNorText_ThrowsArgumentException()
    {
        var chunk = new DocumentChunk(Id: "doc-1");

        Assert.Throws<ArgumentException>(() => chunk.Validate());
    }

    [Test]
    public void Validate_EmptyData_ThrowsArgumentException()
    {
        var chunk = new DocumentChunk(Data: new Dictionary<string, string>());

        Assert.Throws<ArgumentException>(() => chunk.Validate());
    }

    [Test]
    public void Validate_EmptyText_ThrowsArgumentException()
    {
        var chunk = new DocumentChunk(Text: "");

        Assert.Throws<ArgumentException>(() => chunk.Validate());
    }

    [Test]
    public void Validate_WhitespaceOnlyText_ThrowsArgumentException()
    {
        var chunk = new DocumentChunk(Text: "   ");

        Assert.Throws<ArgumentException>(() => chunk.Validate());
    }
}
