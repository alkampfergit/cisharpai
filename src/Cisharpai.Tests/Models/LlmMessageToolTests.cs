using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Tests.Models;

public sealed class LlmMessageToolTests
{
    [Test]
    public void LlmRole_Tool_Exists()
    {
        Assert.That(Enum.IsDefined(typeof(LlmRole), LlmRole.Tool), Is.True);
    }

    [Test]
    public void LlmMessage_BackwardCompatible_WithoutToolProperties()
    {
        var msg = new LlmMessage(LlmRole.User, "Hello");

        Assert.That(msg.Role, Is.EqualTo(LlmRole.User));
        Assert.That(msg.Content, Is.EqualTo("Hello"));
        Assert.That(msg.ToolCallId, Is.Null);
        Assert.That(msg.ToolCalls, Is.Null);
    }

    [Test]
    public void LlmMessage_ToolRole_WithToolCallId()
    {
        var msg = new LlmMessage(LlmRole.Tool, "Weather is sunny", ToolCallId: "call-1");

        Assert.That(msg.Role, Is.EqualTo(LlmRole.Tool));
        Assert.That(msg.Content, Is.EqualTo("Weather is sunny"));
        Assert.That(msg.ToolCallId, Is.EqualTo("call-1"));
    }

    [Test]
    public void LlmMessage_AssistantRole_WithToolCalls()
    {
        var args = JsonDocument.Parse("""{"city":"Paris"}""").RootElement.Clone();
        var toolCalls = new List<ToolCall>
        {
            new("call-1", "get_weather", args)
        };

        var msg = new LlmMessage(LlmRole.Assistant, "", ToolCalls: toolCalls);

        Assert.That(msg.Role, Is.EqualTo(LlmRole.Assistant));
        Assert.That(msg.ToolCalls, Is.Not.Null);
        Assert.That(msg.ToolCalls!.Count, Is.EqualTo(1));
        Assert.That(msg.ToolCalls[0].FunctionName, Is.EqualTo("get_weather"));
    }

    [Test]
    public void LlmMessage_ToolCalls_DefaultsToNull()
    {
        var msg = new LlmMessage(LlmRole.Assistant, "Hello");

        Assert.That(msg.ToolCalls, Is.Null);
    }

    [Test]
    public void LlmMessage_ToolCallId_DefaultsToNull()
    {
        var msg = new LlmMessage(LlmRole.User, "Hello");

        Assert.That(msg.ToolCallId, Is.Null);
    }

    [Test]
    public void LlmMessage_WithToolCalls_IsImmutableRecord()
    {
        var args = JsonDocument.Parse("""{"city":"Paris"}""").RootElement.Clone();
        var toolCalls = new List<ToolCall>
        {
            new("call-1", "get_weather", args)
        };

        var msg = new LlmMessage(LlmRole.Assistant, "", ToolCalls: toolCalls);
        var modified = msg with { Content = "Modified" };

        Assert.That(modified.Content, Is.EqualTo("Modified"));
        Assert.That(modified.ToolCalls, Is.Not.Null);
        Assert.That(msg.Content, Is.EqualTo(""));
    }
}
