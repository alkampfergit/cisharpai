using Cisharpai.Models;

namespace Cisharpai.Tests.Models;

public sealed class ToolChoiceTests
{
    [Test]
    public void Auto_IsNotNull()
    {
        Assert.That(ToolChoice.Auto, Is.Not.Null);
    }

    [Test]
    public void None_IsNotNull()
    {
        Assert.That(ToolChoice.None, Is.Not.Null);
    }

    [Test]
    public void Required_IsNotNull()
    {
        Assert.That(ToolChoice.Required, Is.Not.Null);
    }

    [Test]
    public void Specific_CreatesInstanceWithFunctionName()
    {
        var choice = ToolChoice.Specific("get_weather");

        Assert.That(choice.IsSpecific, Is.True);
        Assert.That(choice.FunctionName, Is.EqualTo("get_weather"));
    }

    [Test]
    public void Auto_IsNotSpecific()
    {
        Assert.That(ToolChoice.Auto.IsSpecific, Is.False);
        Assert.That(ToolChoice.Auto.FunctionName, Is.Null);
    }

    [Test]
    public void None_IsNotSpecific()
    {
        Assert.That(ToolChoice.None.IsSpecific, Is.False);
        Assert.That(ToolChoice.None.FunctionName, Is.Null);
    }

    [Test]
    public void Required_IsNotSpecific()
    {
        Assert.That(ToolChoice.Required.IsSpecific, Is.False);
        Assert.That(ToolChoice.Required.FunctionName, Is.Null);
    }

    [Test]
    public void Auto_Singleton_ReturnsSameInstance()
    {
        Assert.That(ToolChoice.Auto, Is.SameAs(ToolChoice.Auto));
    }

    [Test]
    public void None_Singleton_ReturnsSameInstance()
    {
        Assert.That(ToolChoice.None, Is.SameAs(ToolChoice.None));
    }

    [Test]
    public void Required_Singleton_ReturnsSameInstance()
    {
        Assert.That(ToolChoice.Required, Is.SameAs(ToolChoice.Required));
    }

    [Test]
    public void Specific_EqualInstances_AreEqual()
    {
        var a = ToolChoice.Specific("get_weather");
        var b = ToolChoice.Specific("get_weather");

        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void Specific_DifferentNames_AreNotEqual()
    {
        var a = ToolChoice.Specific("get_weather");
        var b = ToolChoice.Specific("search");

        Assert.That(a, Is.Not.EqualTo(b));
    }
}
