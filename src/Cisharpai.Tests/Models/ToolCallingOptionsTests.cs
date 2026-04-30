using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Tests.Models;

public sealed class ToolCallingOptionsTests
{
    private static readonly JsonElement ValidParameters = JsonDocument.Parse(
        """{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}""")
        .RootElement.Clone();

    private static ToolDefinition ValidTool => new("get_weather", "Get weather", ValidParameters);

    [Test]
    public void Validate_WithTools_DoesNotThrow()
    {
        var options = new ToolCallingOptions(Tools: [ValidTool]);

        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void Validate_EmptyTools_ThrowsArgumentException()
    {
        var options = new ToolCallingOptions(Tools: []);

        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("Tools"));
    }

    [Test]
    public void Validate_NullTools_ThrowsArgumentException()
    {
        var options = new ToolCallingOptions(Tools: null!);

        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("Tools"));
    }

    [Test]
    public void Validate_InvalidTool_PropagatesValidationError()
    {
        var invalidTool = new ToolDefinition("", "desc", ValidParameters);
        var options = new ToolCallingOptions(Tools: [invalidTool]);

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Test]
    public void Validate_MixOfValidAndInvalidTools_ThrowsArgumentException()
    {
        var invalidTool = new ToolDefinition("", "desc", ValidParameters);
        var options = new ToolCallingOptions(Tools: [ValidTool, invalidTool]);

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Test]
    public void ToolChoice_DefaultsToNull()
    {
        var options = new ToolCallingOptions(Tools: [ValidTool]);

        Assert.That(options.ToolChoice, Is.Null);
    }

    [Test]
    public void ToolChoice_CanBeSet()
    {
        var options = new ToolCallingOptions(
            Tools: [ValidTool],
            ToolChoice: ToolChoice.Required);

        Assert.That(options.ToolChoice, Is.SameAs(ToolChoice.Required));
    }
}
