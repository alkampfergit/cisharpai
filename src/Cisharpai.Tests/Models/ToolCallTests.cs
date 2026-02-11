using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Tests.Models;

public sealed class ToolCallTests
{
    [Test]
    public void ToolCall_Properties_AreAccessible()
    {
        var args = JsonDocument.Parse("""{"city":"Paris"}""").RootElement.Clone();
        var call = new ToolCall("call-1", "get_weather", args);

        Assert.That(call.Id, Is.EqualTo("call-1"));
        Assert.That(call.FunctionName, Is.EqualTo("get_weather"));
        Assert.That(call.Arguments.GetProperty("city").GetString(), Is.EqualTo("Paris"));
    }

    [Test]
    public void ToolCall_IsImmutableRecord()
    {
        var args = JsonDocument.Parse("""{"city":"Paris"}""").RootElement.Clone();
        var call = new ToolCall("call-1", "get_weather", args);

        var args2 = JsonDocument.Parse("""{"city":"London"}""").RootElement.Clone();
        var modified = call with { Arguments = args2 };

        Assert.That(modified.Arguments.GetProperty("city").GetString(), Is.EqualTo("London"));
        Assert.That(call.Arguments.GetProperty("city").GetString(), Is.EqualTo("Paris"));
    }
}
