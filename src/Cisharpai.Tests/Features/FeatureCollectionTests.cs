using Cisharpai.Features;

namespace Cisharpai.Tests.Features;

public sealed class FeatureCollectionTests
{
    [Test]
    public void Get_UnregisteredFeature_ReturnsNull()
    {
        var collection = new FeatureCollection();

        var result = collection.Get<ITestFeature>();

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Get_RegisteredFeature_ReturnsInstance()
    {
        var collection = new FeatureCollection();
        var feature = new TestFeature();
        collection.Set<ITestFeature>(feature);

        var result = collection.Get<ITestFeature>();

        Assert.That(result, Is.SameAs(feature));
    }

    [Test]
    public void Set_OverwritesPreviousRegistration()
    {
        var collection = new FeatureCollection();
        var first = new TestFeature();
        var second = new TestFeature();
        collection.Set<ITestFeature>(first);
        collection.Set<ITestFeature>(second);

        var result = collection.Get<ITestFeature>();

        Assert.That(result, Is.SameAs(second));
    }

    [Test]
    public void Set_NullInstance_ThrowsArgumentNullException()
    {
        var collection = new FeatureCollection();

        Assert.Throws<ArgumentNullException>(() => collection.Set<ITestFeature>(null!));
    }

    [Test]
    public void Enumeration_ReturnsAllRegisteredFeatures()
    {
        var collection = new FeatureCollection();
        var feature1 = new TestFeature();
        var feature2 = new AnotherTestFeature();
        collection.Set<ITestFeature>(feature1);
        collection.Set<IAnotherTestFeature>(feature2);

        var entries = collection.ToList();

        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries.Any(e => e.Key == typeof(ITestFeature) && e.Value == feature1), Is.True);
            Assert.That(entries.Any(e => e.Key == typeof(IAnotherTestFeature) && e.Value == feature2), Is.True);
        });
    }

    [Test]
    public void MultipleDifferentFeatures_CanCoexist()
    {
        var collection = new FeatureCollection();
        var feature1 = new TestFeature();
        var feature2 = new AnotherTestFeature();
        collection.Set<ITestFeature>(feature1);
        collection.Set<IAnotherTestFeature>(feature2);

        Assert.Multiple(() =>
        {
            Assert.That(collection.Get<ITestFeature>(), Is.SameAs(feature1));
            Assert.That(collection.Get<IAnotherTestFeature>(), Is.SameAs(feature2));
        });
    }

    [Test]
    public void EmptyCollection_EnumerationReturnsEmpty()
    {
        var collection = new FeatureCollection();

        var entries = collection.ToList();

        Assert.That(entries, Is.Empty);
    }

    // Test helpers
    private interface ITestFeature { }
    private interface IAnotherTestFeature { }
    private sealed class TestFeature : ITestFeature { }
    private sealed class AnotherTestFeature : IAnotherTestFeature { }
}
