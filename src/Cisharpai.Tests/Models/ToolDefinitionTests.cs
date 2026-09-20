using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Tests.Models;

public sealed class ToolDefinitionTests
{
    private static readonly JsonElement ValidParameters = JsonDocument.Parse(
        """{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}""")
        .RootElement.Clone();

    [Test]
    public void Validate_ValidDefinition_DoesNotThrow()
    {
        var tool = new ToolDefinition("get_weather", "Get weather for a city", ValidParameters);

        Assert.DoesNotThrow(() => tool.Validate());
    }

    [Test]
    public void Validate_NullName_ThrowsArgumentException()
    {
        var tool = new ToolDefinition(null!, "desc", ValidParameters);

        var ex = Assert.Throws<ArgumentException>(() => tool.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("Name"));
    }

    [Test]
    public void Validate_EmptyName_ThrowsArgumentException()
    {
        var tool = new ToolDefinition("", "desc", ValidParameters);

        var ex = Assert.Throws<ArgumentException>(() => tool.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("Name"));
    }

    [Test]
    public void Validate_WhitespaceName_ThrowsArgumentException()
    {
        var tool = new ToolDefinition("   ", "desc", ValidParameters);

        var ex = Assert.Throws<ArgumentException>(() => tool.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("Name"));
    }

    [Test]
    public void Validate_ParametersNotObject_ThrowsArgumentException()
    {
        var arrayParams = JsonDocument.Parse("[1,2,3]").RootElement.Clone();
        var tool = new ToolDefinition("test", "desc", arrayParams);

        var ex = Assert.Throws<ArgumentException>(() => tool.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("Parameters"));
    }

    [Test]
    public void Validate_ParametersString_ThrowsArgumentException()
    {
        var stringParams = JsonDocument.Parse("\"hello\"").RootElement.Clone();
        var tool = new ToolDefinition("test", "desc", stringParams);

        var ex = Assert.Throws<ArgumentException>(() => tool.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("Parameters"));
    }

    [Test]
    public void Strict_DefaultsToTrue()
    {
        var tool = new ToolDefinition("test", "desc", ValidParameters);

        Assert.That(tool.Strict, Is.True);
    }

    [Test]
    public void Strict_CanBeSetToFalse()
    {
        var tool = new ToolDefinition("test", "desc", ValidParameters, Strict: false);

        Assert.That(tool.Strict, Is.False);
    }
}
