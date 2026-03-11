using Cisharpai.Models;

namespace Cisharpai.Tests.Models;

public sealed class ToolResultTests
{
    [Test]
    public void ToolResult_Properties_AreAccessible()
    {
        var result = new ToolResult("call-1", "sunny, 22C");

        Assert.Multiple(() =>
        {
            Assert.That(result.ToolCallId, Is.EqualTo("call-1"));
            Assert.That(result.Content, Is.EqualTo("sunny, 22C"));
            Assert.That(result.IsError, Is.False);
        });
    }

    [Test]
    public void IsError_DefaultsToFalse()
    {
        var result = new ToolResult("call-1", "data");

        Assert.That(result.IsError, Is.False);
    }

    [Test]
    public void IsError_CanBeSetToTrue()
    {
        var result = new ToolResult("call-1", "something went wrong", IsError: true);

        Assert.That(result.IsError, Is.True);
    }

    [Test]
    public void ToolResult_IsImmutableRecord()
    {
        var result = new ToolResult("call-1", "data");
        var modified = result with { Content = "new data" };

        Assert.Multiple(() =>
        {
            Assert.That(modified.Content, Is.EqualTo("new data"));
            Assert.That(result.Content, Is.EqualTo("data"));
        });
    }
}
