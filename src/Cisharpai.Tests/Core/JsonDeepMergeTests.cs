using System.Text.Json;

namespace Cisharpai.Tests.Core;

public sealed class JsonDeepMergeTests
{
    [Test]
    public void Merge_AddsNewTopLevelProperty()
    {
        var baseJson = """{"model":"gpt-4","temperature":0.7}""";
        var overrides = JsonDocument.Parse("""{"top_p":0.9}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("gpt-4"));
        Assert.That(doc.RootElement.GetProperty("temperature").GetDouble(), Is.EqualTo(0.7));
        Assert.That(doc.RootElement.GetProperty("top_p").GetDouble(), Is.EqualTo(0.9));
    }

    [Test]
    public void Merge_OverridesExistingScalarProperty()
    {
        var baseJson = """{"model":"gpt-4","temperature":0.7}""";
        var overrides = JsonDocument.Parse("""{"temperature":0.3}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("temperature").GetDouble(), Is.EqualTo(0.3));
    }

    [Test]
    public void Merge_DeepMergesNestedObjects()
    {
        var baseJson = """{"model":"gpt-5","reasoning":{"effort":"high"}}""";
        var overrides = JsonDocument.Parse("""{"reasoning":{"summary":"auto"}}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        var reasoning = doc.RootElement.GetProperty("reasoning");
        Assert.That(reasoning.GetProperty("effort").GetString(), Is.EqualTo("high"));
        Assert.That(reasoning.GetProperty("summary").GetString(), Is.EqualTo("auto"));
    }

    [Test]
    public void Merge_OverrideReplacesObjectWithScalar()
    {
        var baseJson = """{"reasoning":{"effort":"high"}}""";
        var overrides = JsonDocument.Parse("""{"reasoning":"none"}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("reasoning").GetString(), Is.EqualTo("none"));
    }

    [Test]
    public void Merge_OverrideReplacesScalarWithObject()
    {
        var baseJson = """{"reasoning":"none"}""";
        var overrides = JsonDocument.Parse("""{"reasoning":{"effort":"high"}}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("reasoning").GetProperty("effort").GetString(), Is.EqualTo("high"));
    }

    [Test]
    public void Merge_OverrideReplacesArray()
    {
        var baseJson = """{"tools":["search","code"]}""";
        var overrides = JsonDocument.Parse("""{"tools":["browse"]}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        var tools = doc.RootElement.GetProperty("tools");
        Assert.That(tools.GetArrayLength(), Is.EqualTo(1));
        Assert.That(tools[0].GetString(), Is.EqualTo("browse"));
    }

    [Test]
    public void Merge_OverrideCanSetNullValue()
    {
        var baseJson = """{"model":"gpt-4","temperature":0.7}""";
        var overrides = JsonDocument.Parse("""{"temperature":null}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("temperature").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public void Merge_EmptyOverrides_ReturnsOriginal()
    {
        var baseJson = """{"model":"gpt-4","temperature":0.7}""";
        var overrides = JsonDocument.Parse("{}").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("gpt-4"));
        Assert.That(doc.RootElement.GetProperty("temperature").GetDouble(), Is.EqualTo(0.7));
    }

    [Test]
    public void Merge_EmptyBase_ReturnsOverrides()
    {
        var baseJson = "{}";
        var overrides = JsonDocument.Parse("""{"model":"gpt-4"}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("gpt-4"));
    }

    [Test]
    public void Merge_MultipleNewProperties()
    {
        var baseJson = """{"model":"gpt-4"}""";
        var overrides = JsonDocument.Parse("""{"temperature":0.5,"top_p":0.9,"stream":true}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("gpt-4"));
        Assert.That(doc.RootElement.GetProperty("temperature").GetDouble(), Is.EqualTo(0.5));
        Assert.That(doc.RootElement.GetProperty("top_p").GetDouble(), Is.EqualTo(0.9));
        Assert.That(doc.RootElement.GetProperty("stream").GetBoolean(), Is.True);
    }

    [Test]
    public void Merge_DeeplyNestedMerge_ThreeLevels()
    {
        var baseJson = """{"a":{"b":{"c":"original","d":1}}}""";
        var overrides = JsonDocument.Parse("""{"a":{"b":{"c":"overridden","e":2}}}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        var b = doc.RootElement.GetProperty("a").GetProperty("b");
        Assert.That(b.GetProperty("c").GetString(), Is.EqualTo("overridden"));
        Assert.That(b.GetProperty("d").GetInt32(), Is.EqualTo(1));
        Assert.That(b.GetProperty("e").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public void Merge_OverrideBooleanProperty()
    {
        var baseJson = """{"stream":false}""";
        var overrides = JsonDocument.Parse("""{"stream":true}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("stream").GetBoolean(), Is.True);
    }

    [Test]
    public void Merge_OverrideIntegerProperty()
    {
        var baseJson = """{"max_tokens":100}""";
        var overrides = JsonDocument.Parse("""{"max_tokens":500}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("max_tokens").GetInt32(), Is.EqualTo(500));
    }

    [Test]
    public void Merge_PreservesPropertyOrder_BaseFirst()
    {
        var baseJson = """{"a":1,"b":2}""";
        var overrides = JsonDocument.Parse("""{"c":3}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        var props = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.That(props, Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void Merge_ThrowsWhenOverridesNotObject()
    {
        var baseJson = """{"model":"gpt-4"}""";
        var overrides = JsonDocument.Parse("[1,2,3]").RootElement;

        Assert.Throws<ArgumentException>(() => JsonDeepMerge.Merge(baseJson, overrides));
    }

    [Test]
    public void Merge_ThrowsWhenBaseNotObject()
    {
        var baseJson = "[1,2,3]";
        var overrides = JsonDocument.Parse("""{"a":1}""").RootElement;

        Assert.Throws<ArgumentException>(() => JsonDeepMerge.Merge(baseJson, overrides));
    }

    [Test]
    public void Merge_ComplexRealWorldScenario_VerbosityPassThrough()
    {
        var baseJson = """
        {
            "model": "gpt-5",
            "input": [{"role":"user","content":"Hello"}],
            "max_output_tokens": 1000,
            "reasoning": {"effort": "medium"}
        }
        """;

        var overrides = JsonDocument.Parse("""
        {
            "text": {"verbosity": "high"},
            "reasoning": {"effort": "low", "summary": "auto"}
        }
        """).RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("model").GetString(), Is.EqualTo("gpt-5"));
        Assert.That(doc.RootElement.GetProperty("max_output_tokens").GetInt32(), Is.EqualTo(1000));
        Assert.That(doc.RootElement.GetProperty("text").GetProperty("verbosity").GetString(), Is.EqualTo("high"));
        Assert.That(doc.RootElement.GetProperty("reasoning").GetProperty("effort").GetString(), Is.EqualTo("low"));
        Assert.That(doc.RootElement.GetProperty("reasoning").GetProperty("summary").GetString(), Is.EqualTo("auto"));

        var input = doc.RootElement.GetProperty("input");
        Assert.That(input.GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public void Merge_NestedObjectWithNewProperty_InBase()
    {
        var baseJson = """{"config":{"timeout":30}}""";
        var overrides = JsonDocument.Parse("""{"config":{"retries":3}}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        var config = doc.RootElement.GetProperty("config");
        Assert.That(config.GetProperty("timeout").GetInt32(), Is.EqualTo(30));
        Assert.That(config.GetProperty("retries").GetInt32(), Is.EqualTo(3));
    }

    [Test]
    public void Merge_OverrideStringWithNumber()
    {
        var baseJson = """{"value":"text"}""";
        var overrides = JsonDocument.Parse("""{"value":42}""").RootElement;

        var result = JsonDeepMerge.Merge(baseJson, overrides);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("value").GetInt32(), Is.EqualTo(42));
    }
}
