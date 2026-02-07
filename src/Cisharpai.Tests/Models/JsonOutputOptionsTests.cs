using Cisharpai.Models;

namespace Cisharpai.Tests.Models;

public sealed class JsonOutputOptionsTests
{
    [Test]
    public void JsonMode_WithNoSchema_IsValid()
    {
        var options = new JsonOutputOptions(Mode: JsonOutputMode.JsonMode);

        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void JsonMode_WithSchemaProvided_IsValid()
    {
        var options = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonMode,
            SchemaName: "test",
            JsonSchema: """{"type":"object"}""");

        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void JsonSchema_WithSchemaAndName_IsValid()
    {
        var options = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "test_schema",
            JsonSchema: """{"type":"object","properties":{"name":{"type":"string"}}}""");

        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void JsonSchema_WithoutSchemaName_ThrowsArgumentException()
    {
        var options = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            JsonSchema: """{"type":"object"}""");

        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("SchemaName"));
    }

    [Test]
    public void JsonSchema_WithoutJsonSchema_ThrowsArgumentException()
    {
        var options = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "test");

        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("JsonSchema"));
    }

    [Test]
    public void JsonSchema_WithBothMissing_ThrowsArgumentException()
    {
        var options = new JsonOutputOptions(Mode: JsonOutputMode.JsonSchema);

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Test]
    public void JsonSchema_DefaultStrictIsTrue()
    {
        var options = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "test",
            JsonSchema: """{"type":"object"}""");

        Assert.That(options.Strict, Is.True);
    }

    [Test]
    public void JsonSchema_StrictCanBeSetFalse()
    {
        var options = new JsonOutputOptions(
            Mode: JsonOutputMode.JsonSchema,
            SchemaName: "test",
            JsonSchema: """{"type":"object"}""",
            Strict: false);

        Assert.That(options.Strict, Is.False);
        Assert.DoesNotThrow(() => options.Validate());
    }
}
